using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Heimdall.Core.Entities;
using Heimdall.Core.Services.Cache;
using Heimdall.Core.Services.Repository;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public class WikiPageDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> FilePaths { get; set; } = new();
    public string Importance { get; set; } = "medium";
    public List<string> RelatedPages { get; set; } = new();
    public string? ParentId { get; set; }
    public bool? IsSection { get; set; }
    public List<string>? Children { get; set; }
}

public class WikiStructureDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<WikiPageDto> Pages { get; set; } = new();
    public List<WikiSection> Sections { get; set; } = new();
    public List<string> RootSections { get; set; } = new();
}

public class WikiSection
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Pages { get; set; } = new();
    public List<string>? Subsections { get; set; }
}

public class WikiGenerationResult
{
    public bool FromCache { get; set; }
    public RepoInfo? Repo { get; set; }
    public string Language { get; set; } = "zh";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public WikiStructureDto WikiStructure { get; set; } = new();
    public Dictionary<string, WikiPageDto> GeneratedPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Error { get; set; }
}

public sealed class WikiTaskService
{
    private readonly TaskLlmService _taskLlm;
    private readonly TaskPromptService _taskPrompt;
    private readonly RepositoryAccessService _repoAccess;
    private readonly WikiCacheService _wikiCache;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<WikiTaskService> _logger;

    public WikiTaskService(
        TaskLlmService taskLlm,
        TaskPromptService taskPrompt,
        RepositoryAccessService repoAccess,
        WikiCacheService wikiCache,
        IHostApplicationLifetime appLifetime,
        ILogger<WikiTaskService> logger)
    {
        _taskLlm = taskLlm;
        _taskPrompt = taskPrompt;
        _repoAccess = repoAccess;
        _wikiCache = wikiCache;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    public async Task<WikiGenerationResult> GenerateAsync(
        string repoUrl, string repoType, string? token,
        string? provider, string? model, string? customModel,
        string language, bool comprehensive, bool forceRefresh,
        Guid? repositoryId, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, _appLifetime.ApplicationStopping, ct);
        var execToken = linkedCts.Token;

        _logger.LogInformation(
            "开始生成 Wiki RequestId={Id} Url={Url} Type={Type} Provider={Provider} Model={Model}",
            requestId, repoUrl, repoType, provider, model ?? customModel);

        // 检查缓存
        if (!forceRefresh && repositoryId.HasValue)
        {
            var cached = await _wikiCache.GetAsync(repositoryId.Value, "main", language);
            if (cached is not null && cached.Pages.Any())
            {
                _logger.LogInformation("命中缓存 WikiId={WikiId}", cached.Id);
                return BuildFromCache(cached);
            }
        }

        // 准备仓库
        var prepareStopwatch = Stopwatch.StartNew();
        var repoPath = await _repoAccess.PrepareRepositoryAsync(repoUrl, repoType, token, execToken);
        var localStructure = _repoAccess.GetLocalStructure(repoPath);
        _logger.LogInformation("仓库准备完成 Path={Path} Files={Count} Elapsed={Ms}ms",
            repoPath, localStructure.FileTree.Split('\n').Length, prepareStopwatch.ElapsedMilliseconds);

        // 生成 Wiki 结构
        var resolvedLang = language == "zh" ? "中文" : "English";
        var structurePrompt = _taskPrompt.BuildWikiStructurePrompt(
            ExtractRepoName(repoUrl), ExtractRepoName(repoUrl),
            localStructure.FileTree, localStructure.Readme, resolvedLang, comprehensive);

        _logger.LogInformation("开始生成 Wiki 结构 RequestId={Id}", requestId);
        var structureResponse = await _taskLlm.GenerateTextAsync(
            provider ?? "ollama", model, customModel, structurePrompt, execToken);
        var wikiStructure = ParseWikiStructure(structureResponse, comprehensive, warnings);

        // 生成每个页面
        var generatedPages = new Dictionary<string, WikiPageDto>(StringComparer.OrdinalIgnoreCase);
        var (resolvedProvider, resolvedModel) = _taskLlm.ResolveTarget(provider, model, customModel);

        for (var i = 0; i < wikiStructure.Pages.Count; i++)
        {
            execToken.ThrowIfCancellationRequested();
            var page = wikiStructure.Pages[i];

            _logger.LogInformation("生成页面 {Index}/{Total} PageId={Id} Title={Title}",
                i + 1, wikiStructure.Pages.Count, page.Id, page.Title);

            var pagePrompt = _taskPrompt.BuildWikiPagePrompt(
                page.Id, page.Title, page.Description,
                ExtractRepoName(repoUrl), ExtractRepoName(repoUrl),
                repoType, repoUrl, localStructure.FileTree, resolvedLang);

            try
            {
                var content = await _taskLlm.GenerateTextAsync(
                    provider ?? "ollama", model, customModel, pagePrompt, execToken);
                page.Content = WikiMarkdownNormalizer.Normalize(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "页面生成失败 PageId={Id}", page.Id);
                page.Content = $"# {page.Title}\n\n> 生成失败：{ex.Message}\n\n{page.Description}";
            }

            generatedPages[page.Id] = page;
        }

        // 保存到数据库
        if (repositoryId.HasValue)
        {
            var wiki = new Wiki
            {
                SourceRepositoryId = repositoryId.Value,
                SourceBranch = "main",
                Language = language,
                Title = wikiStructure.Title,
                Description = wikiStructure.Description
            };

            var entityPages = generatedPages.Select((kv, idx) => new WikiPage
            {
                Title = kv.Value.Title,
                ContentMarkdown = kv.Value.Content,
                PageOrder = idx,
                Importance = kv.Value.Importance,
                FilePaths = kv.Value.FilePaths?.ToArray()
            }).ToList();

            await _wikiCache.SaveAsync(wiki, entityPages);
        }

        _logger.LogInformation("Wiki 生成完成 RequestId={Id} Pages={Count} Elapsed={Ms}ms",
            requestId, generatedPages.Count, totalStopwatch.ElapsedMilliseconds);

        return new WikiGenerationResult
        {
            FromCache = false,
            Repo = new RepoInfo { Owner = ExtractRepoName(repoUrl), Repo = ExtractRepoName(repoUrl), Type = repoType, RepoUrl = repoUrl },
            Language = language,
            Provider = resolvedProvider,
            Model = resolvedModel,
            WikiStructure = wikiStructure,
            GeneratedPages = generatedPages
        };
    }

    private WikiGenerationResult BuildFromCache(Wiki wiki)
    {
        var pages = new Dictionary<string, WikiPageDto>(StringComparer.OrdinalIgnoreCase);
        var structurePages = new List<WikiPageDto>();

        foreach (var p in wiki.Pages)
        {
            var dto = new WikiPageDto
            {
                Id = p.Id.ToString(),
                Title = p.Title,
                Description = "",
                Content = p.ContentMarkdown ?? "",
                Importance = p.Importance,
                ParentId = p.ParentPageId?.ToString()
            };
            pages[dto.Id] = dto;
            structurePages.Add(dto);
        }

        return new WikiGenerationResult
        {
            FromCache = true,
            Language = wiki.Language,
            WikiStructure = new WikiStructureDto
            {
                Id = wiki.Id.ToString(),
                Title = wiki.Title,
                Description = wiki.Description ?? "",
                Pages = structurePages
            },
            GeneratedPages = pages
        };
    }

    private WikiStructureDto ParseWikiStructure(string response, bool comprehensive, List<string> warnings)
    {
        try
        {
            var cleaned = WikiMarkdownNormalizer.Normalize(response);
            var match = Regex.Match(cleaned, "<wiki_structure>[\\s\\S]*?</wiki_structure>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                warnings.Add("LLM 未返回有效 Wiki XML 结构");
                return BuildFallbackStructure(cleaned);
            }

            var doc = XDocument.Parse(match.Value);
            var root = doc.Root!;

            var pages = root.Element("pages")?.Elements("page").Select(p => new WikiPageDto
            {
                Id = p.Attribute("id")?.Value ?? "",
                Title = p.Element("title")?.Value.Trim() ?? "",
                Description = p.Element("description")?.Value.Trim() ?? "",
                Importance = NormalizeImportance(p.Element("importance")?.Value),
                FilePaths = p.Element("relevant_files")?.Elements("file_path")
                    .Select(f => f.Value.Trim()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList() ?? new(),
                RelatedPages = p.Element("related_pages")?.Elements("related")
                    .Select(r => r.Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new(),
                ParentId = p.Element("parent_section")?.Value.Trim()
            }).Where(p => !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Title)).ToList() ?? new();

            var sections = root.Element("sections")?.Elements("section").Select(s => new WikiSection
            {
                Id = s.Attribute("id")?.Value ?? "",
                Title = s.Element("title")?.Value.Trim() ?? "",
                Pages = s.Element("pages")?.Elements("page_ref")
                    .Select(p => p.Value.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new(),
                Subsections = s.Element("subsections")?.Elements("section_ref")
                    .Select(r => r.Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
            }).Where(s => !string.IsNullOrWhiteSpace(s.Id)).ToList() ?? new();

            return new WikiStructureDto
            {
                Id = "wiki",
                Title = root.Element("title")?.Value.Trim() ?? "Repository Wiki",
                Description = root.Element("description")?.Value.Trim() ?? "",
                Pages = pages,
                Sections = sections,
                RootSections = sections.Select(s => s.Id).ToList()
            };
        }
        catch (Exception ex)
        {
            warnings.Add($"XML 解析失败：{ex.Message}");
            return BuildFallbackStructure(response);
        }
    }

    private WikiStructureDto BuildFallbackStructure(string text)
    {
        var preview = text.Length > 500 ? text[..500] : text;
        return new WikiStructureDto
        {
            Id = "wiki",
            Title = "Repository Wiki",
            Description = "LLM 返回内容未能解析为有效的 Wiki 结构。以下是原始响应摘要：\n" + preview,
            Pages = new List<WikiPageDto>
            {
                new() { Id = "overview", Title = "仓库概览", Description = preview, Importance = "high" }
            }
        };
    }

    private static string NormalizeImportance(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };

    private static string ExtractRepoName(string url)
    {
        if (Directory.Exists(url)) return new DirectoryInfo(url).Name;
        return url.TrimEnd('/').Split('/').Last().Replace(".git", "", StringComparison.OrdinalIgnoreCase);
    }
}
