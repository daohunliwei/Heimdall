using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Cache;
using Heimdall.Api.Services.Chat;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Repository;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// Wiki 任务服务，负责后端主导的 Wiki 结构与页面生成。
/// </summary>
public sealed class WikiTaskService
{
    private readonly ChatOrchestratorService _chatOrchestratorService;
    private readonly HeimdallConfigService _configService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<WikiTaskService> _logger;
    private readonly RepositoryAccessService _repositoryAccessService;
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly TaskRequestUtilityService _taskRequestUtilityService;
    private readonly WikiCacheService _wikiCacheService;

    /// <summary>
    /// 初始化 Wiki 任务服务。
    /// </summary>
    public WikiTaskService(
        ChatOrchestratorService chatOrchestratorService,
        HeimdallConfigService configService,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<WikiTaskService> logger,
        RepositoryAccessService repositoryAccessService,
        TaskLlmService taskLlmService,
        TaskPromptService taskPromptService,
        TaskRequestUtilityService taskRequestUtilityService,
        WikiCacheService wikiCacheService)
    {
        _chatOrchestratorService = chatOrchestratorService;
        _configService = configService;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _repositoryAccessService = repositoryAccessService;
        _taskLlmService = taskLlmService;
        _taskPromptService = taskPromptService;
        _taskRequestUtilityService = taskRequestUtilityService;
        _wikiCacheService = wikiCacheService;
    }

    /// <summary>
    /// 生成 Wiki 内容。
    /// </summary>
    public async Task<WikiTaskResponse> GenerateAsync(WikiTaskRequest request, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();
        var repo = _taskRequestUtilityService.BuildRepoInfo(request);
        var language = _taskRequestUtilityService.ResolveLanguage(request);
        var warnings = new List<string>();
        var taskTimeout = _configService.GetWikiTaskTimeout();
        using var taskTimeoutCts = new CancellationTokenSource(taskTimeout);
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(
            taskTimeoutCts.Token,
            _hostApplicationLifetime.ApplicationStopping);
        var executionToken = executionCts.Token;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
                _logger.LogWarning(
                    "收到前端取消信号，但后端继续执行 Wiki 任务 RequestId={RequestId}",
                    requestId));
        }

        _logger.LogInformation(
            "开始生成 Wiki RequestId={RequestId} Repo={Owner}/{Repo} Type={RepoType} Provider={Provider} Model={Model} ForceRefresh={ForceRefresh} Comprehensive={Comprehensive} TaskTimeoutMinutes={TaskTimeoutMinutes}",
            requestId,
            repo.Owner,
            repo.Repo,
            repo.Type,
            request.Provider,
            request.CustomModel ?? request.Model,
            request.ForceRefresh,
            request.Comprehensive,
            taskTimeout.TotalMinutes);

        if (!request.ForceRefresh)
        {
            var cached = await _wikiCacheService.GetAsync(repo.Owner, repo.Repo, repo.Type, language);
            if (cached is not null &&
                cached.WikiStructure.Pages.Count > 0 &&
                cached.GeneratedPages.Count > 0)
            {
                var normalizedPages = WikiMarkdownNormalizer.NormalizePages(cached.GeneratedPages);
                return new WikiTaskResponse
                {
                    FromCache = true,
                    Repo = cached.Repo ?? repo,
                    Language = cached.Language,
                    Provider = cached.Provider,
                    Model = cached.Model,
                    WikiStructure = EnsureSections(cached.WikiStructure),
                    GeneratedPages = normalizedPages,
                    Debug = new WikiTaskDebugInfo
                    {
                        RequestId = requestId,
                        GeneratedPageCount = normalizedPages.Count,
                        StructurePageCount = cached.WikiStructure.Pages.Count,
                        Warnings = ["命中缓存结果"]
                    }
                };
            }
        }

        var resolvedRepoUrl = _taskRequestUtilityService.ResolveRepoUrl(request);
        if (string.IsNullOrWhiteSpace(resolvedRepoUrl))
        {
            throw new InvalidOperationException("缺少有效的仓库地址或本地目录。");
        }

        var prepareStopwatch = Stopwatch.StartNew();
        var repositoryPath = await _repositoryAccessService.PrepareRepositoryAsync(resolvedRepoUrl, repo.Type, request.Token, executionToken);
        var localStructure = _repositoryAccessService.GetLocalStructure(repositoryPath);
        _logger.LogInformation(
            "仓库准备完成 RequestId={RequestId} Path={RepositoryPath} FileCount={FileCount} ReadmeLength={ReadmeLength} ElapsedMs={ElapsedMs}",
            requestId,
            repositoryPath,
            CountFileTreeEntries(localStructure.FileTree),
            localStructure.Readme.Length,
            prepareStopwatch.ElapsedMilliseconds);
        var languageDisplayName = _taskRequestUtilityService.ResolveLanguageDisplayName(request);
        var structurePrompt = _taskPromptService.BuildWikiStructurePrompt(
            repo.Owner,
            repo.Repo,
            localStructure.FileTree,
            localStructure.Readme,
            languageDisplayName,
            request.Comprehensive);

        var structureStopwatch = Stopwatch.StartNew();
        var structureResponse = await _taskLlmService.GenerateTextAsync(request, structurePrompt, executionToken);
        var structurePreview = BuildStructurePreview(structureResponse);
        var wikiStructure = BuildWikiStructureWithFallback(
            repo,
            localStructure,
            request.Comprehensive,
            structureResponse,
            warnings,
            requestId);

        _logger.LogInformation(
            "结构阶段完成 RequestId={RequestId} StructurePageCount={StructurePageCount} SectionCount={SectionCount} ElapsedMs={ElapsedMs}",
            requestId,
            wikiStructure.Pages.Count,
            wikiStructure.Sections.Count,
            structureStopwatch.ElapsedMilliseconds);

        wikiStructure = EnsureRenderableStructure(wikiStructure, warnings);
        var generatedPages = new Dictionary<string, WikiPage>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < wikiStructure.Pages.Count; index++)
        {
            executionToken.ThrowIfCancellationRequested();
            var page = wikiStructure.Pages[index];
            var pageStopwatch = Stopwatch.StartNew();

            var pagePrompt = _taskPromptService.BuildWikiPagePrompt(
                page,
                wikiStructure.Pages,
                repo.Owner,
                repo.Repo,
                repo.Type,
                repo.RepoUrl,
                languageDisplayName);

            var pageRequest = _taskRequestUtilityService.BuildChatRequest(
                request,
                new[]
                {
                    new ChatMessage
                    {
                        Role = "user",
                        Content = pagePrompt
                    }
                });

            try
            {
                _logger.LogInformation(
                    "开始生成页面 RequestId={RequestId} PageIndex={PageIndex} TotalPages={TotalPages} PageId={PageId} Title={Title}",
                    requestId,
                    index + 1,
                    wikiStructure.Pages.Count,
                    page.Id,
                    page.Title);

                var content = await _chatOrchestratorService.GenerateAsync(pageRequest, executionToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("模型返回了空页面内容");
                }

                generatedPages[page.Id] = new WikiPage
                {
                    Id = page.Id,
                    Title = page.Title,
                    Description = page.Description,
                    Content = WikiMarkdownNormalizer.Normalize(content),
                    FilePaths = page.FilePaths,
                    Importance = page.Importance,
                    RelatedPages = page.RelatedPages,
                    ParentId = page.ParentId,
                    IsSection = page.IsSection,
                    Children = page.Children
                };

                _logger.LogInformation(
                    "页面生成完成 RequestId={RequestId} PageId={PageId} Title={Title} ElapsedMs={ElapsedMs} ContentLength={ContentLength}",
                    requestId,
                    page.Id,
                    page.Title,
                    pageStopwatch.ElapsedMilliseconds,
                    generatedPages[page.Id].Content.Length);
            }
            catch (Exception exception)
            {
                var cancellationReason = DescribeCancellation(taskTimeoutCts, executionToken, exception);
                _logger.LogWarning(
                    exception,
                    "页面生成失败，写入占位内容 RequestId={RequestId} PageId={PageId} Title={Title} ElapsedMs={ElapsedMs} CancellationReason={CancellationReason}",
                    requestId,
                    page.Id,
                    page.Title,
                    pageStopwatch.ElapsedMilliseconds,
                    cancellationReason);

                warnings.Add($"页面 `{page.Title}` 生成失败，已写入占位内容：{exception.Message}（{cancellationReason}）");
                generatedPages[page.Id] = CreateFallbackGeneratedPage(page, exception.Message);
            }
        }

        var (providerId, model) = _taskLlmService.ResolveTarget(request);
        var sanitizedRepo = new RepoInfo
        {
            Owner = repo.Owner,
            Repo = repo.Repo,
            Type = repo.Type,
            RepoUrl = repo.RepoUrl,
            Token = null,
            LocalPath = repo.LocalPath
        };
        var response = new WikiTaskResponse
        {
            FromCache = false,
            Repo = sanitizedRepo,
            Language = language,
            Provider = providerId,
            Model = model,
            WikiStructure = wikiStructure,
            GeneratedPages = generatedPages,
            Debug = new WikiTaskDebugInfo
            {
                RequestId = requestId,
                RepositoryPath = repositoryPath,
                FileCount = CountFileTreeEntries(localStructure.FileTree),
                StructurePageCount = wikiStructure.Pages.Count,
                GeneratedPageCount = generatedPages.Count,
                FallbackUsed = warnings.Count > 0,
                StructureResponsePreview = structurePreview,
                Warnings = warnings
            }
        };

        _logger.LogInformation(
            "Wiki 任务执行完成 RequestId={RequestId} GeneratedPages={GeneratedPages} Warnings={Warnings} ElapsedMs={ElapsedMs}",
            requestId,
            generatedPages.Count,
            warnings.Count,
            totalStopwatch.ElapsedMilliseconds);

        await _wikiCacheService.SaveAsync(new WikiCacheSaveRequest
        {
            Repo = sanitizedRepo,
            Language = language,
            Provider = providerId,
            Model = model,
            WikiStructure = wikiStructure,
            GeneratedPages = generatedPages
        });

        return response;
    }

    private string DescribeCancellation(CancellationTokenSource taskTimeoutCts, CancellationToken executionToken, Exception exception)
    {
        if (exception is TimeoutException)
        {
            return "provider-timeout";
        }

        if (taskTimeoutCts.IsCancellationRequested)
        {
            return "task-timeout";
        }

        if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            return "application-stopping";
        }

        if (executionToken.IsCancellationRequested)
        {
            return "execution-token-cancelled";
        }

        return "not-cancelled";
    }

    private static string StripThinkTags(string content)
    {
        return Regex.Replace(content, "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static string? ExtractWikiStructureXml(string responseText)
    {
        var normalized = StripThinkTags(responseText)
            .Replace("```xml", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();
        var match = Regex.Match(normalized, "<wiki_structure>[\\s\\S]*?</wiki_structure>", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string BuildWikiStructureError(string responseText)
    {
        var normalized = StripThinkTags(responseText);
        var condensed = Regex.Replace(normalized, "\\s+", " ").Trim();
        var preview = condensed.Length > 300 ? condensed[..300] : condensed;

        if (string.IsNullOrWhiteSpace(preview))
        {
            return "模型返回为空，请检查 Provider、模型和 API Key 配置后重试。";
        }

        if (preview.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase) &&
            (preview.Contains("must be set", StringComparison.OrdinalIgnoreCase) ||
             preview.Contains("not configured", StringComparison.OrdinalIgnoreCase)))
        {
            return "未配置 `OPENAI_API_KEY`，请先补充嵌入模型所需的环境变量后再重试。";
        }

        return $"模型未返回有效的 Wiki XML，原始响应：{preview}";
    }

    private WikiStructure BuildWikiStructureWithFallback(
        RepoInfo repo,
        LocalRepoStructureResponse localStructure,
        bool isComprehensiveView,
        string structureResponse,
        List<string> warnings,
        string requestId)
    {
        var xmlText = ExtractWikiStructureXml(structureResponse);
        if (string.IsNullOrWhiteSpace(xmlText))
        {
            var message = BuildWikiStructureError(structureResponse);
            warnings.Add($"结构阶段未返回有效 XML，已使用后端兜底：{message}");
            _logger.LogWarning(
                "结构阶段未返回有效 XML，启用兜底 RequestId={RequestId} Repo={Repo}",
                requestId,
                repo.Repo);
            return BuildFallbackWikiStructure(repo, localStructure, isComprehensiveView);
        }

        try
        {
            return EnsureSections(ParseWikiStructure(xmlText, isComprehensiveView));
        }
        catch (Exception exception)
        {
            warnings.Add($"结构 XML 解析失败，已使用后端兜底：{exception.Message}");
            _logger.LogWarning(
                exception,
                "结构 XML 解析失败，启用兜底 RequestId={RequestId} Repo={Repo}",
                requestId,
                repo.Repo);
            return BuildFallbackWikiStructure(repo, localStructure, isComprehensiveView);
        }
    }

    private static WikiStructure ParseWikiStructure(string xmlText, bool isComprehensiveView)
    {
        var sanitized = SanitizeWikiStructureXml(xmlText);
        XDocument document;

        try
        {
            document = XDocument.Parse(sanitized, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            sanitized = RepairCommonWikiStructureXmlIssues(sanitized);
            document = XDocument.Parse(sanitized, LoadOptions.PreserveWhitespace);
        }

        var root = document.Root ?? throw new XmlException("缺少 wiki_structure 根节点。");

        var sections = root.Element("sections")?
            .Elements("section")
            .Select(sectionElement => new WikiSection
            {
                Id = sectionElement.Attribute("id")?.Value ?? string.Empty,
                Title = sectionElement.Element("title")?.Value?.Trim() ?? string.Empty,
                Pages = sectionElement.Element("pages")?
                    .Elements("page_ref")
                    .Select(item => item.Value.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList() ?? new List<string>(),
                Subsections = sectionElement.Element("subsections")?
                    .Elements("section_ref")
                    .Select(item => item.Value.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToList() ?? new List<WikiSection>();

        var pages = root.Element("pages")?
            .Elements("page")
            .Select(pageElement => new WikiPage
            {
                Id = pageElement.Attribute("id")?.Value ?? string.Empty,
                Title = pageElement.Element("title")?.Value?.Trim() ?? string.Empty,
                Description = pageElement.Element("description")?.Value?.Trim() ?? string.Empty,
                Importance = NormalizeImportance(pageElement.Element("importance")?.Value),
                FilePaths = pageElement.Element("relevant_files")?
                    .Elements("file_path")
                    .Select(item => item.Value.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>(),
                RelatedPages = pageElement.Element("related_pages")?
                    .Elements("related")
                    .Select(item => item.Value.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>(),
                ParentId = pageElement.Element("parent_section")?.Value?.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title))
            .ToList() ?? new List<WikiPage>();

        var rootSections = sections
            .Where(section => !sections.Any(other => other.Subsections?.Any(item => string.Equals(item, section.Id, StringComparison.OrdinalIgnoreCase)) == true))
            .Select(section => section.Id)
            .ToList();

        return new WikiStructure
        {
            Id = "wiki",
            Title = root.Element("title")?.Value?.Trim() ?? "Repository Wiki",
            Description = root.Element("description")?.Value?.Trim() ?? string.Empty,
            Pages = pages,
            Sections = isComprehensiveView ? sections : new List<WikiSection>(),
            RootSections = isComprehensiveView ? rootSections : new List<string>()
        };
    }

    private static string SanitizeWikiStructureXml(string xmlText)
    {
        return Regex.Replace(xmlText, "&(?![a-zA-Z]+;|#\\d+;|#x[0-9a-fA-F]+;)", "&amp;");
    }

    private static string RepairCommonWikiStructureXmlIssues(string xmlText)
    {
        var repaired = xmlText;

        repaired = Regex.Replace(
            repaired,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</page>)",
            "$1</parent_section>$2",
            RegexOptions.IgnoreCase);

        repaired = Regex.Replace(
            repaired,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</related_pages>)",
            "$1</parent_section>$2",
            RegexOptions.IgnoreCase);

        return repaired;
    }

    private static string NormalizeImportance(string? importance)
    {
        return importance?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };
    }

    private static WikiStructure EnsureSections(WikiStructure wikiStructure)
    {
        if (wikiStructure.Sections.Count > 0 && wikiStructure.RootSections.Count > 0)
        {
            return wikiStructure;
        }

        var sectionGroups = wikiStructure.Pages
            .GroupBy(page => string.IsNullOrWhiteSpace(page.ParentId) ? "default-section" : page.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sections = sectionGroups
            .Select(group => new WikiSection
            {
                Id = group.Key,
                Title = group.Key.Equals("default-section", StringComparison.OrdinalIgnoreCase) ? "Pages" : group.Key,
                Pages = group.Select(page => page.Id).ToList()
            })
            .ToList();

        return new WikiStructure
        {
            Id = wikiStructure.Id,
            Title = wikiStructure.Title,
            Description = wikiStructure.Description,
            Sections = sections,
            RootSections = sections.Select(section => section.Id).ToList(),
            Pages = wikiStructure.Pages
                .Select(page => new WikiPage
                {
                    Id = page.Id,
                    Title = page.Title,
                    Description = page.Description,
                    Content = page.Content,
                    FilePaths = page.FilePaths,
                    Importance = page.Importance,
                    RelatedPages = page.RelatedPages,
                    ParentId = string.IsNullOrWhiteSpace(page.ParentId) ? "default-section" : page.ParentId,
                    IsSection = page.IsSection,
                    Children = page.Children
                })
                .ToList()
        };
    }

    private static WikiStructure EnsureRenderableStructure(WikiStructure wikiStructure, List<string> warnings)
    {
        if (wikiStructure.Pages.Count > 0)
        {
            return wikiStructure;
        }

        if (wikiStructure.Sections.Count == 0)
        {
            warnings.Add("结构结果没有页面也没有章节，已创建默认概览页");
            var overviewPage = CreateStructurePlaceholderPage(
                "page-overview",
                "仓库概览",
                wikiStructure.Description,
                null);

            return EnsureSections(new WikiStructure
            {
                Id = wikiStructure.Id,
                Title = wikiStructure.Title,
                Description = wikiStructure.Description,
                Pages = [overviewPage]
            });
        }

        warnings.Add("结构结果缺少页面，已按章节自动补占位页");
        var pages = wikiStructure.Sections
            .Select(section => CreateStructurePlaceholderPage(
                $"page-{section.Id}",
                section.Title,
                $"这是 `{section.Title}` 的兜底页面，原始结构未返回可直接展示的页面内容",
                section.Id))
            .ToList();

        var sectionMap = pages.ToDictionary(page => page.ParentId!, page => page.Id, StringComparer.OrdinalIgnoreCase);
        var sections = wikiStructure.Sections
            .Select(section => new WikiSection
            {
                Id = section.Id,
                Title = section.Title,
                Pages = section.Pages.Count > 0
                    ? section.Pages
                    : sectionMap.TryGetValue(section.Id, out var pageId) ? [pageId] : new List<string>(),
                Subsections = section.Subsections
            })
            .ToList();

        return new WikiStructure
        {
            Id = wikiStructure.Id,
            Title = wikiStructure.Title,
            Description = wikiStructure.Description,
            Pages = pages,
            Sections = sections,
            RootSections = wikiStructure.RootSections.Count > 0
                ? wikiStructure.RootSections
                : sections.Select(section => section.Id).ToList()
        };
    }

    private static WikiStructure BuildFallbackWikiStructure(RepoInfo repo, LocalRepoStructureResponse localStructure, bool isComprehensiveView)
    {
        var topEntries = localStructure.FileTree
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? item)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(isComprehensiveView ? 6 : 3)
            .ToList();

        var pages = new List<WikiPage>
        {
            CreateStructurePlaceholderPage(
                "page-overview",
                "仓库概览",
                $"仓库 `{repo.Owner}/{repo.Repo}` 的结构生成未完整返回，当前展示后端兜底概览页",
                "section-overview")
        };

        var sections = new List<WikiSection>
        {
            new()
            {
                Id = "section-overview",
                Title = "仓库概览",
                Pages = ["page-overview"]
            }
        };

        foreach (var entry in topEntries)
        {
            var normalizedEntry = Regex.Replace(entry.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(normalizedEntry))
            {
                normalizedEntry = $"entry-{sections.Count}";
            }

            var sectionId = $"section-{normalizedEntry}";
            var pageId = $"page-{normalizedEntry}";
            sections.Add(new WikiSection
            {
                Id = sectionId,
                Title = entry,
                Pages = [pageId]
            });
            pages.Add(CreateStructurePlaceholderPage(
                pageId,
                entry,
                $"该页面由后端兜底生成，用于保留 `{entry}` 的导航结构",
                sectionId));
        }

        return new WikiStructure
        {
            Id = "wiki",
            Title = $"{repo.Repo} Wiki",
            Description = string.IsNullOrWhiteSpace(localStructure.Readme)
                ? $"仓库 `{repo.Owner}/{repo.Repo}` 的自动生成文档"
                : ExtractReadmeSummary(localStructure.Readme),
            Pages = pages,
            Sections = sections,
            RootSections = sections.Select(section => section.Id).ToList()
        };
    }

    private static WikiPage CreateStructurePlaceholderPage(string id, string title, string description, string? parentId)
    {
        return new WikiPage
        {
            Id = id,
            Title = title,
            Description = description,
            Content = string.Empty,
            ParentId = parentId,
            Importance = "medium"
        };
    }

    private static WikiPage CreateFallbackGeneratedPage(WikiPage page, string reason)
    {
        var fileHint = page.FilePaths.Count > 0
            ? string.Join("\n", page.FilePaths.Select(path => $"- `{path}`"))
            : "- 当前结构未提供相关文件";

        var content = $"""
# {page.Title}

> 该页面由后端兜底生成，原因：{reason}

## 页面说明

{(string.IsNullOrWhiteSpace(page.Description) ? "当前页面暂无模型生成内容" : page.Description)}

## 调试提示

- 结构已经保留，前端不会丢失当前导航状态
- 可以保留当前 Provider/Model 配置后再次刷新重试
- 如需定位问题，请查看响应中的 `debug.warnings`

## 相关文件

{fileHint}
""";

        return new WikiPage
        {
            Id = page.Id,
            Title = page.Title,
            Description = page.Description,
            Content = content,
            FilePaths = page.FilePaths,
            Importance = page.Importance,
            RelatedPages = page.RelatedPages,
            ParentId = page.ParentId,
            IsSection = page.IsSection,
            Children = page.Children
        };
    }

    private static string BuildStructurePreview(string responseText)
    {
        var normalized = StripThinkTags(responseText);
        var condensed = Regex.Replace(normalized, "\\s+", " ").Trim();
        return condensed.Length > 600 ? condensed[..600] : condensed;
    }

    private static int CountFileTreeEntries(string fileTree)
    {
        return fileTree
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private static string ExtractReadmeSummary(string readme)
    {
        var lines = readme
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Take(3)
            .ToList();

        if (lines.Count == 0)
        {
            return "仓库 README 未提供可提取的摘要，当前展示后端兜底结构";
        }

        return string.Join(' ', lines);
    }
}
