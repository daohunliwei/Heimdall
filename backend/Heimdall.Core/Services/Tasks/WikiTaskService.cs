using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Cache;
using Heimdall.Core.Services.Repository;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class WikiTaskService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskLlmService _taskLlm;
    private readonly TaskPromptService _taskPrompt;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<WikiTaskService> _logger;

    public WikiTaskService(
        IServiceScopeFactory scopeFactory,
        TaskLlmService taskLlm,
        TaskPromptService taskPrompt,
        IHostApplicationLifetime appLifetime,
        ILogger<WikiTaskService> logger)
    {
        _scopeFactory = scopeFactory;
        _taskLlm = taskLlm;
        _taskPrompt = taskPrompt;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    /// <summary>
    /// 步骤 1：创建任务记录并立即返回 task_id。
    /// </summary>
    public async Task<TaskRecord> CreateTaskAsync(
        string repoUrl, string repoType, string? token,
        string? provider, string? model, string? customModel,
        string language, bool comprehensive, bool forceRefresh,
        Guid? userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var repoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

        // 确保仓库记录存在
        var repoOwner = ExtractRepoName(repoUrl);
        var repoName = ExtractRepoName(repoUrl);
        var existingRepo = await repoRepo.GetByOwnerRepoTypeAsync(repoOwner, repoName, repoType);
        Guid? repositoryId;

        if (existingRepo is not null)
        {
            repositoryId = existingRepo.Id;
        }
        else
        {
            var newRepo = new Core.Entities.Repository
            {
                Owner = repoOwner,
                RepoName = repoName,
                RepoType = repoType,
                RepoUrl = repoUrl,
                DefaultBranch = "main",
                DefaultLanguage = language
            };
            await repoRepo.AddAsync(newRepo);
            repositoryId = newRepo.Id;
        }

        // 计算去重哈希
        var hashInput = $"{repositoryId}|main|wiki|{provider}|{model ?? customModel}|{language}|{comprehensive}";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();

        // 检查是否有 running/pending 任务
        var running = await taskRepo.GetRunningByRepoAndBranchAsync(repositoryId.Value, "main");
        if (running is not null) return running;

        var pending = await taskRepo.GetPendingByRepoBranchTypeAsync(repositoryId.Value, "main", "wiki");
        if (pending is not null) return pending;

        var task = new TaskRecord
        {
            TaskType = "wiki",
            Status = "pending",
            RepositoryId = repositoryId,
            SourceBranch = "main",
            UserId = userId,
            RequestHash = requestHash,
            Provider = provider,
            Model = model ?? customModel,
            Language = language,
            ProgressPercent = 0,
            ProgressMessage = "任务已创建，等待执行..."
        };

        var created = await taskRepo.EnqueueAsync(task);
        _logger.LogInformation("任务已创建 TaskId={TaskId} Repo={Owner}/{Repo}", created.Id, repoOwner, repoName);
        return created;
    }

    /// <summary>
    /// 步骤 2：后台执行 Wiki 生成（由 TaskQueueService 调用）。
    /// </summary>
    public async Task ExecuteAsync(TaskRecord task, string repoUrl, string repoType, string? token,
        string? provider, string? model, string? customModel, string language, bool comprehensive, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, _appLifetime.ApplicationStopping, ct);
        var execToken = linkedCts.Token;

        try
        {
            // 更新任务状态为 running
            await UpdateTaskAsync(task.Id, t =>
            {
                t.Status = "running";
                t.StartedAt = DateTime.UtcNow;
                t.ProgressPercent = 5;
                t.ProgressMessage = "正在克隆仓库...";
            });

            // 1. 准备仓库
            using var execScope = _scopeFactory.CreateScope();
            var repoAccess = execScope.ServiceProvider.GetRequiredService<RepositoryAccessService>();
            var repoPath = await repoAccess.PrepareRepositoryAsync(repoUrl, repoType, token, execToken);
            var localStructure = repoAccess.GetLocalStructure(repoPath);
            var fileCount = localStructure.FileTree.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

            await UpdateTaskAsync(task.Id, t =>
            {
                t.ProgressPercent = 15;
                t.ProgressMessage = $"仓库准备完成，共 {fileCount} 个文件";
            });

            _logger.LogInformation("仓库准备完成 TaskId={TaskId} Files={Count} Path={Path}", task.Id, fileCount, repoPath);

            // 2. 生成 Wiki 结构
            var langDisplay = language == "zh" ? "中文" : "English";
            var repoName = ExtractRepoName(repoUrl);
            var structurePrompt = _taskPrompt.BuildWikiStructurePrompt(
                repoName, repoName, localStructure.FileTree, localStructure.Readme, langDisplay, comprehensive);

            await UpdateTaskAsync(task.Id, t =>
            {
                t.ProgressPercent = 20;
                t.ProgressMessage = "正在生成 Wiki 结构...";
            });

            var structureSw = Stopwatch.StartNew();
            var structureResponse = await _taskLlm.GenerateTextAsync(provider, model, customModel, structurePrompt, execToken);

            // 记录 LLM 调用日志
            await LogLlmCallAsync(task.Id, 0, "structure_generation", provider, model ?? customModel,
                structurePrompt, structureResponse, (int)structureSw.ElapsedMilliseconds, false);

            var wikiStructure = ParseWikiStructure(structureResponse, comprehensive);

            await UpdateTaskAsync(task.Id, t =>
            {
                t.ProgressPercent = 35;
                t.ProgressMessage = $"Wiki 结构生成完成，共 {wikiStructure.Pages.Count} 个页面";
            });

            // 3. 逐页生成内容
            var totalPages = wikiStructure.Pages.Count;
            for (var i = 0; i < totalPages; i++)
            {
                execToken.ThrowIfCancellationRequested();
                var page = wikiStructure.Pages[i];
                var percent = 35 + (int)(55.0 * (i + 1) / totalPages);

                await UpdateTaskAsync(task.Id, t =>
                {
                    t.ProgressPercent = percent;
                    t.ProgressMessage = $"正在生成页面 {i + 1}/{totalPages}: {page.Title}";
                });

                var pagePrompt = _taskPrompt.BuildWikiPagePrompt(
                    page, wikiStructure.Pages, repoName, repoName, repoType, repoUrl, langDisplay);

                var pageSw = Stopwatch.StartNew();
                try
                {
                    var pageContent = await _taskLlm.GenerateTextAsync(provider, model, customModel, pagePrompt, execToken);

                    await LogLlmCallAsync(task.Id, i + 1, "page_generation", provider, model ?? customModel,
                        pagePrompt, pageContent, (int)pageSw.ElapsedMilliseconds, false);

                    page.Content = WikiMarkdownNormalizer.Normalize(pageContent);

                    // 逐页落库
                    await SaveWikiPageAsync(task.Id, i, page);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "页面生成失败 Page={Title}", page.Title);
                    page.Content = $"# {page.Title}\n\n> 生成失败：{ex.Message}\n\n{page.Description}";
                    await LogLlmCallAsync(task.Id, i + 1, "page_generation", provider, model ?? customModel,
                        pagePrompt, page.Content, (int)pageSw.ElapsedMilliseconds, true, ex.Message);
                }
            }

            // 4. 保存完整 Wiki 到数据库
            await SaveWikiAsync(task, wikiStructure, repoUrl, repoType, language);

            // 5. 标记完成
            await UpdateTaskAsync(task.Id, t =>
            {
                t.Status = "completed";
                t.ProgressPercent = 100;
                t.ProgressMessage = $"Wiki 生成完成，共 {totalPages} 个页面";
                t.CompletedAt = DateTime.UtcNow;
            });

            // 保存生成结果 JSON
            await SaveResultJsonAsync(task.Id, wikiStructure);

            _logger.LogInformation("Wiki 生成完成 TaskId={TaskId} Pages={Count} Elapsed={Ms}ms",
                task.Id, totalPages, totalStopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskAsync(task.Id, t =>
            {
                t.Status = "cancelled";
                t.ErrorMessage = "任务已取消";
                t.CompletedAt = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki 生成失败 TaskId={TaskId}", task.Id);
            await UpdateTaskAsync(task.Id, t =>
            {
                t.Status = "failed";
                t.ErrorMessage = ex.Message;
                t.CompletedAt = DateTime.UtcNow;
            });
        }
    }

    private async Task UpdateTaskAsync(Guid taskId, Action<TaskRecord> update)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var existing = await taskRepo.GetByIdAsync(taskId);
        if (existing is not null)
        {
            update(existing);
            await taskRepo.UpdateStatusAsync(existing.Id, existing.Status,
                existing.ProgressPercent, existing.ProgressMessage, existing.ErrorMessage);
        }
    }

    private async Task LogLlmCallAsync(Guid taskId, int stepOrder, string callType,
        string? provider, string? model, string prompt, string response, int latencyMs,
        bool isError, string? errorMsg = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<ITaskLlmCallLogRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        var promptTokens = prompt.Length / 4;
        var completionTokens = response.Length / 4;

        var log = new TaskLlmCallLog
        {
            TaskId = taskId,
            StepOrder = stepOrder,
            CallType = callType,
            Provider = provider,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            RequestPreview = prompt.Length > 500 ? prompt[..500] : prompt,
            ResponsePreview = response.Length > 500 ? response[..500] : response,
            LatencyMs = latencyMs,
            IsError = isError,
            ErrorMessage = errorMsg
        };

        await logRepo.AddAsync(log);

        // 更新累计 token
        var task = await taskRepo.GetByIdAsync(taskId);
        if (task is not null)
        {
            task.TotalPromptTokens += promptTokens;
            task.TotalCompletionTokens += completionTokens;
            await taskRepo.UpdateStatusAsync(task.Id, task.Status,
                task.ProgressPercent, task.ProgressMessage, task.ErrorMessage);
        }
    }

    private async Task SaveWikiPageAsync(Guid taskId, int pageOrder, WikiPageDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var pageRepo = scope.ServiceProvider.GetRequiredService<IWikiPageRepository>();

        // 查找或创建 page 记录
        // 这里简单处理：直接写入。实际生产中应有 upsert 逻辑
        var page = new WikiPage
        {
            TaskId = taskId,
            PageOrder = pageOrder,
            Title = dto.Title,
            ContentMarkdown = dto.Content,
            Importance = dto.Importance,
            FilePaths = dto.FilePaths?.ToArray()
        };
        await pageRepo.AddAsync(page);
    }

    private async Task SaveWikiAsync(TaskRecord task, WikiStructureDto structure,
        string repoUrl, string repoType, string language)
    {
        using var scope = _scopeFactory.CreateScope();
        var wikiRepo = scope.ServiceProvider.GetRequiredService<IWikiRepository>();
        var pageRepo = scope.ServiceProvider.GetRequiredService<IWikiPageRepository>();

        var repoOwner = ExtractRepoName(repoUrl);
        var repoName = ExtractRepoName(repoUrl);

        // 确保 repository 存在
        var repoConfigRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();
        var repo = await repoConfigRepo.GetByOwnerRepoTypeAsync(repoOwner, repoName, repoType);
        if (repo is null)
        {
            repo = new Core.Entities.Repository
            {
                Owner = repoOwner,
                RepoName = repoName,
                RepoType = repoType,
                RepoUrl = repoUrl
            };
            await repoConfigRepo.AddAsync(repo);
        }

        // 查找已有 wiki
        var existing = await wikiRepo.GetByRepoBranchLanguageAsync(repo.Id, "main", language);
        Wiki wiki;
        if (existing is not null)
        {
            wiki = existing;
            wiki.Title = structure.Title;
            wiki.Description = structure.Description;
            wiki.UpdatedAt = DateTime.UtcNow;
            await wikiRepo.UpdateAsync(wiki);
            await pageRepo.DeleteByWikiIdAsync(wiki.Id);
        }
        else
        {
            wiki = new Wiki
            {
                SourceRepositoryId = repo.Id,
                SourceBranch = "main",
                Language = language,
                Title = structure.Title,
                Description = structure.Description
            };
            await wikiRepo.AddAsync(wiki);
        }

        // 保存所有页面
        foreach (var (idx, dto) in structure.Pages.Select((p, i) => (i, p)))
        {
            await pageRepo.AddAsync(new WikiPage
            {
                WikiId = wiki.Id,
                TaskId = task.Id,
                PageOrder = idx,
                Title = dto.Title,
                ContentMarkdown = dto.Content,
                Importance = dto.Importance,
                FilePaths = dto.FilePaths?.ToArray()
            });
        }

        _logger.LogInformation("Wiki 已保存 WikiId={WikiId} Pages={Count}", wiki.Id, structure.Pages.Count);
    }

    private async Task SaveResultJsonAsync(Guid taskId, WikiStructureDto structure)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var task = await taskRepo.GetByIdAsync(taskId);
        if (task is not null)
        {
            task.ResultJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                structure.Title,
                structure.Description,
                PageCount = structure.Pages.Count,
                Pages = structure.Pages.Select(p => new { p.Id, p.Title, p.Importance, ContentLength = p.Content.Length })
            });
            await taskRepo.UpdateStatusAsync(task.Id, task.Status, task.ProgressPercent, task.ProgressMessage, task.ErrorMessage);
        }
    }

    // ============ XML 解析（从原始 WikiTaskService 恢复） ============

    private WikiStructureDto ParseWikiStructure(string response, bool comprehensive)
    {
        try
        {
            var cleaned = WikiMarkdownNormalizer.Normalize(response);
            var match = Regex.Match(cleaned, "<wiki_structure>[\\s\\S]*?</wiki_structure>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                _logger.LogWarning("LLM 未返回有效 Wiki XML，使用兜底结构");
                return BuildFallbackStructure(response);
            }

            var xml = SanitizeXml(match.Value);
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                xml = RepairXmlIssues(xml);
                doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }

            var root = doc.Root!;
            var sections = root.Element("sections")?.Elements("section").Select(s => new WikiSectionDto
            {
                Id = s.Attribute("id")?.Value ?? "",
                Title = s.Element("title")?.Value.Trim() ?? "",
                Pages = s.Element("pages")?.Elements("page_ref").Select(p => p.Value.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new(),
                Subsections = s.Element("subsections")?.Elements("section_ref").Select(r => r.Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
            }).Where(s => !string.IsNullOrWhiteSpace(s.Id)).ToList() ?? new();

            var pages = root.Element("pages")?.Elements("page").Select(p => new WikiPageDto
            {
                Id = p.Attribute("id")?.Value ?? "",
                Title = p.Element("title")?.Value.Trim() ?? "",
                Description = p.Element("description")?.Value.Trim() ?? "",
                Importance = NormalizeImportance(p.Element("importance")?.Value),
                FilePaths = p.Element("relevant_files")?.Elements("file_path").Select(f => f.Value.Trim()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList() ?? new(),
                RelatedPages = p.Element("related_pages")?.Elements("related").Select(r => r.Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new(),
                ParentId = p.Element("parent_section")?.Value.Trim()
            }).Where(p => !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Title)).ToList() ?? new();

            // 确保有 section
            if (sections.Count == 0 && pages.Count > 0)
            {
                sections = new List<WikiSectionDto>
                {
                    new() { Id = "default-section", Title = "Pages", Pages = pages.Select(p => p.Id).ToList() }
                };
            }

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
            _logger.LogWarning(ex, "XML 解析失败，使用兜底");
            return BuildFallbackStructure(response);
        }
    }

    private static string SanitizeXml(string xml) =>
        Regex.Replace(xml, "&(?![a-zA-Z]+;|#\\d+;|#x[0-9a-fA-F]+;)", "&amp;");

    private static string RepairXmlIssues(string xml)
    {
        return Regex.Replace(
            Regex.Replace(xml, "(<parent_section>\\s*[^<]*?)</section>(\\s*</page>)", "$1</parent_section>$2", RegexOptions.IgnoreCase),
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</related_pages>)", "$1</parent_section>$2", RegexOptions.IgnoreCase);
    }

    private static string NormalizeImportance(string? value) =>
        value?.Trim().ToLowerInvariant() switch { "high" => "high", "low" => "low", _ => "medium" };

    private WikiStructureDto BuildFallbackStructure(string response)
    {
        var preview = response.Length > 300 ? response[..300] : response;
        return new WikiStructureDto
        {
            Id = "wiki",
            Title = "Repository Wiki",
            Description = $"LLM 返回内容未能解析为有效的 Wiki 结构。原始响应摘要：{preview}",
            Pages = new List<WikiPageDto>
            {
                new() { Id = "overview", Title = "仓库概览", Description = preview, Importance = "high" }
            },
            Sections = new List<WikiSectionDto>
            {
                new() { Id = "default-section", Title = "仓库概览", Pages = new List<string> { "overview" } }
            },
            RootSections = new List<string> { "default-section" }
        };
    }

    private static string ExtractRepoName(string url)
    {
        if (Directory.Exists(url)) return new DirectoryInfo(url).Name;
        return url.TrimEnd('/').Split('/').Last().Replace(".git", "", StringComparison.OrdinalIgnoreCase);
    }
}
