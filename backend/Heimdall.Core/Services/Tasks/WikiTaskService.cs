using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Rag;
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
    private readonly RepositoryAccessService _repoAccess;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<WikiTaskService> _logger;

    public WikiTaskService(
        IServiceScopeFactory scopeFactory,
        TaskLlmService taskLlm,
        TaskPromptService taskPrompt,
        RepositoryAccessService repoAccess,
        IHostApplicationLifetime appLifetime,
        ILogger<WikiTaskService> logger)
    {
        _scopeFactory = scopeFactory;
        _taskLlm = taskLlm;
        _taskPrompt = taskPrompt;
        _repoAccess = repoAccess;
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
        Guid? userId,
        string branch = "main",
        string refreshStrategy = "latest",
        string generationProfile = "comprehensive")
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var repoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

        // 确保仓库记录存在
        var source = _repoAccess.FindSource(repoType, repoUrl);
        var (repoOwner, repoName) = source.ParseOwnerRepo(repoUrl);
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
                DefaultBranch = branch,
                DefaultLanguage = language
            };
            await repoRepo.AddAsync(newRepo);
            repositoryId = newRepo.Id;
        }

        // 计算去重哈希
        var hashInput = $"{repositoryId}|{branch}|wiki|{provider}|{model ?? customModel}|{language}|{comprehensive}|{generationProfile}";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();

        // 检查是否有 running/pending 任务
        var running = await taskRepo.GetRunningByRepoAndBranchAsync(repositoryId.Value, branch);
        if (running is not null) return running;

        var pending = await taskRepo.GetPendingByRepoBranchTypeAsync(repositoryId.Value, branch, "wiki");
        if (pending is not null) return pending;

        // 非强制刷新时，已完成任务直接返回（防止重复生成覆盖已有数据）
        if (!forceRefresh)
        {
            var completed = await taskRepo.GetCompletedByHashAsync(requestHash);
            if (completed is not null) return completed;
        }

        var task = new TaskRecord
        {
            TaskType = "wiki",
            Status = "pending",
            RepositoryId = repositoryId,
            SourceBranch = branch,
            UserId = userId,
            RequestHash = requestHash,
            Provider = provider,
            Model = model ?? customModel,
            Language = language,
            ProgressPercent = 0,
            ProgressMessage = "任务已创建，等待执行...",
            // V2 版本字段
            TargetBranch = branch,
            RefreshStrategy = refreshStrategy,
            ForceRefresh = forceRefresh,
            ConfigHash = requestHash
        };

        var created = await taskRepo.EnqueueAsync(task);
        _logger.LogInformation("任务已创建 TaskId={TaskId} Repo={Owner}/{Repo}", created.Id, repoOwner, repoName);
        return created;
    }

    /// <summary>
    /// 步骤 2：后台执行 Wiki 生成（由 TaskQueueService 调用）。
    /// </summary>
    public async Task ExecuteAsync(TaskRecord task, string repoUrl, string repoType, string? token,
        string? provider, string? model, string? customModel, string language, bool comprehensive, CancellationToken ct,
        string branch = "main", string generationProfile = "comprehensive")
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
            var (execOwner, execRepo) = repoAccess.FindSource(repoType, repoUrl).ParseOwnerRepo(repoUrl);
            var structurePrompt = _taskPrompt.BuildWikiStructurePrompt(
                execOwner, execRepo, localStructure.FileTree, localStructure.Readme, langDisplay, comprehensive,
                generationProfile);

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

            // 3. 先创建 Wiki 记录（确保页面保存时有有效的 WikiId）
            Guid wikiId;
            using (var wikiScope = _scopeFactory.CreateScope())
            {
                var wikiRepository = wikiScope.ServiceProvider.GetRequiredService<IWikiRepository>();
                var pageRepository = wikiScope.ServiceProvider.GetRequiredService<IWikiPageRepository>();
                var repoConfigRepo = wikiScope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

                var repoEntity = await repoConfigRepo.GetByOwnerRepoTypeAsync(execOwner, execRepo, repoType);
                if (repoEntity is null)
                {
                    repoEntity = new Core.Entities.Repository
                    {
                        Owner = execOwner,
                        RepoName = execRepo,
                        RepoType = repoType,
                        RepoUrl = repoUrl
                    };
                    await repoConfigRepo.AddAsync(repoEntity);
                }

                var existingWiki = await wikiRepository.GetByRepoBranchLanguageAsync(repoEntity.Id, branch, language);
                if (existingWiki is not null)
                {
                    await pageRepository.DeleteByWikiIdAsync(existingWiki.Id);
                    existingWiki.Title = wikiStructure.Title;
                    existingWiki.Description = wikiStructure.Description;
                    existingWiki.UpdatedAt = DateTime.UtcNow;
                    await wikiRepository.UpdateAsync(existingWiki);
                    wikiId = existingWiki.Id;
                }
                else
                {
                    var wiki = new Wiki
                    {
                        SourceRepositoryId = repoEntity.Id,
                        SourceBranch = branch,
                        Language = language,
                        Title = wikiStructure.Title,
                        Description = wikiStructure.Description
                    };
                    await wikiRepository.AddAsync(wiki);
                    wikiId = wiki.Id;
                }
            }

            _logger.LogInformation("Wiki 记录已创建 WikiId={WikiId}", wikiId);

            // 4. 逐页生成内容（含实际文件内容 + RAG 检索）
            var totalPages = wikiStructure.Pages.Count;

            // 4a. 后台启动双向量嵌入流水线（写入 code_embedding_chunks + wiki_embedding_chunks）
            _ = Task.Run(async () =>
            {
                try
                {
                    using var embedScope = _scopeFactory.CreateScope();

                    // V2: 写入 code_embedding_chunks
                    var repoAccess2 = embedScope.ServiceProvider.GetRequiredService<RepositoryAccessService>();
                    var codeEmbedService = embedScope.ServiceProvider.GetRequiredService<ICodeEmbeddingService>();
                    var versionRepo = embedScope.ServiceProvider.GetRequiredService<IRepositoryVersionRepository>();

                    var repoVersion = await versionRepo.GetLatestByRepoBranchAsync(task.RepositoryId!.Value, branch);
                    if (repoVersion is not null)
                    {
                        var documents = await repoAccess2.ReadRepositoryDocumentsAsync(
                            repoPath, new(), new(), new(), new(), CancellationToken.None);
                        var codeChunkCount = await codeEmbedService.EmbedRepositoryAsync(repoVersion.Id, documents, CancellationToken.None);
                        _logger.LogInformation("代码嵌入完成 TaskId={TaskId} VersionId={VersionId} Chunks={Count}",
                            task.Id, repoVersion.Id, codeChunkCount);
                    }

                    _logger.LogInformation("嵌入流水线完成 TaskId={TaskId}", task.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "嵌入流水线失败（非致命）TaskId={TaskId}", task.Id);
                }
            });

            var pageIdMapping = new Dictionary<string, Guid>(); // string_id → DB GUID

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

                // 读取页面关联的实际文件内容
                var fileContents = ReadPageFiles(repoPath, page.FilePaths);

                var pagePrompt = _taskPrompt.BuildWikiPagePrompt(
                    page, wikiStructure.Pages, execOwner, execRepo, repoType, repoUrl,
                    langDisplay, fileContents);

                var pageSw = Stopwatch.StartNew();
                try
                {
                    var pageContent = await _taskLlm.GenerateTextAsync(provider, model, customModel, pagePrompt, execToken);

                    await LogLlmCallAsync(task.Id, i + 1, "page_generation", provider, model ?? customModel,
                        pagePrompt, pageContent, (int)pageSw.ElapsedMilliseconds, false);

                    page.Content = WikiMarkdownNormalizer.Normalize(pageContent);

                    // 逐页落库，记录 string_id → GUID 映射
                    var pageGuid = await SaveWikiPageAsync(task.Id, wikiId, i, page);
                    if (pageGuid.HasValue)
                    {
                        pageIdMapping[page.Id] = pageGuid.Value;
                    }
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

            // V2: 创建版本记录（RepositoryVersion + WikiVersion）并回写任务
            await EnsureV2VersionRecordsAsync(task, wikiId, execOwner, execRepo, repoType, language, branch, generationProfile, structureResponse);

            // 保存页面关系（wiki_page_relations）
            if (task.ResultWikiVersionId.HasValue)
            {
                await SaveWikiPageRelationsAsync(task.ResultWikiVersionId.Value, wikiStructure, pageIdMapping);
            }

            // 5a. 后台启动 Wiki 内容嵌入（写入 wiki_embedding_chunks）
            _ = Task.Run(async () =>
            {
                try
                {
                    using var embedScope = _scopeFactory.CreateScope();
                    var wikiEmbedService = embedScope.ServiceProvider.GetRequiredService<IWikiEmbeddingService>();
                    var wikiVersionRepo = embedScope.ServiceProvider.GetRequiredService<IWikiVersionRepository>();
                    var pageRepo2 = embedScope.ServiceProvider.GetRequiredService<IWikiPageRepository>();

                    if (task.ResultWikiVersionId.HasValue)
                    {
                        var wikiVersion = await wikiVersionRepo.GetByIdAsync(task.ResultWikiVersionId.Value);
                        if (wikiVersion is not null)
                        {
                            var allPages = await pageRepo2.GetByWikiIdAsync(wikiId);
                            var versionPages = allPages.Where(p => p.WikiVersionId == wikiVersion.Id).ToList();
                            var wikiChunkCount = await wikiEmbedService.EmbedWikiPagesAsync(wikiVersion.Id, versionPages, CancellationToken.None);
                            _logger.LogInformation("Wiki 嵌入完成 TaskId={TaskId} VersionId={VersionId} Chunks={Count}",
                                task.Id, wikiVersion.Id, wikiChunkCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wiki 嵌入流水线失败（非致命）TaskId={TaskId}", task.Id);
                }
            });

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
            RequestPreview = prompt,
            ResponsePreview = response,
            LatencyMs = latencyMs,
            IsError = isError,
            ErrorMessage = errorMsg
        };

        await logRepo.AddAsync(log);

        // 使用原子 SQL 更新累计 token，避免跨 scope 并发冲突
        await taskRepo.IncrementTokensAsync(taskId, promptTokens, completionTokens);
    }

    private async Task<Guid?> SaveWikiPageAsync(Guid taskId, Guid wikiId, int pageOrder, WikiPageDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var pageRepo = scope.ServiceProvider.GetRequiredService<IWikiPageRepository>();

        var page = new WikiPage
        {
            WikiId = wikiId,
            TaskId = taskId,
            PageOrder = pageOrder,
            Title = dto.Title,
            ContentMarkdown = dto.Content,
            Importance = dto.Importance,
            FilePaths = dto.FilePaths?.ToArray()
        };
        var saved = await pageRepo.AddAsync(page);
        return saved.Id;
    }

    private async Task SaveWikiAsync(TaskRecord task, WikiStructureDto structure,
        string repoUrl, string repoType, string language)
    {
        // Wiki 和页面已在 ExecuteAsync 中创建/落库，此处仅做最终校验
        using var scope = _scopeFactory.CreateScope();
        var wikiRepo = scope.ServiceProvider.GetRequiredService<IWikiRepository>();
        var repoConfigRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

        var (repoOwner, repoName) = _repoAccess.FindSource(repoType, repoUrl).ParseOwnerRepo(repoUrl);
        var repo = await repoConfigRepo.GetByOwnerRepoTypeAsync(repoOwner, repoName, repoType);
        if (repo is null)
        {
            _logger.LogWarning("SaveWikiAsync: 仓库记录不存在 Owner={Owner} Repo={Repo}", repoOwner, repoName);
            return;
        }

        var wiki = await wikiRepo.GetByRepoBranchLanguageAsync(repo.Id, task.TargetBranch ?? "main", language);
        if (wiki is not null)
        {
            wiki.Title = structure.Title;
            wiki.Description = structure.Description;
            wiki.UpdatedAt = DateTime.UtcNow;
            await wikiRepo.UpdateAsync(wiki);
        }

        _logger.LogInformation("Wiki 已更新 WikiId={WikiId}", wiki?.Id);
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

    /// <summary>
    /// V2: 确保版本记录存在（RepositoryVersion + WikiSpace + WikiVersion），并回写任务关联。
    /// </summary>
    private async Task EnsureV2VersionRecordsAsync(TaskRecord task, Guid wikiId,
        string execOwner, string execRepo, string repoType, string language,
        string branch = "main", string generationProfile = "comprehensive",
        string? structureJson = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repoConfigRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();
            var versionRepo = scope.ServiceProvider.GetRequiredService<IRepositoryVersionRepository>();
            var spaceRepo = scope.ServiceProvider.GetRequiredService<IWikiSpaceRepository>();
            var wikiVersionRepo = scope.ServiceProvider.GetRequiredService<IWikiVersionRepository>();
            var pageRepo = scope.ServiceProvider.GetRequiredService<IWikiPageRepository>();

            // 1. 查找仓库记录
            var repo = await repoConfigRepo.GetByOwnerRepoTypeAsync(execOwner, execRepo, repoType);
            if (repo is null) return;

            // 2. 确保 RepositoryVersion 存在
            var repoVersion = await versionRepo.GetLatestByRepoBranchAsync(repo.Id, branch);
            if (repoVersion is null)
            {
                repoVersion = new RepositoryVersion
                {
                    RepositoryId = repo.Id,
                    BranchName = branch,
                    CommitSha = "unknown",
                    CommitTime = DateTime.UtcNow,
                    CommitAuthor = "system",
                    CommitMessage = $"由任务 {task.Id} 触发生成",
                    SourceStatus = "active",
                    IsLatestOnBranch = true,
                    VersionSourceConfidence = "unknown"
                };
                repoVersion = await versionRepo.AddAsync(repoVersion);
            }

            // 3. 确保 WikiSpace 存在
            var wikiSpace = await spaceRepo.GetByRepoLangViewAsync(repo.Id, language, "default");
            if (wikiSpace is null)
            {
                wikiSpace = new WikiSpace
                {
                    RepositoryId = repo.Id,
                    Language = language,
                    ViewType = "default",
                    Title = $"{repo.DisplayName} Wiki",
                    Description = $"为 {repo.DisplayName} 生成的 Wiki"
                };
                wikiSpace = await spaceRepo.AddAsync(wikiSpace);
            }

            // 4. 创建 WikiVersion
            var versionNo = await wikiVersionRepo.CountBySpaceIdAsync(wikiSpace.Id) + 1;
            var wikiVersion = new WikiVersion
            {
                WikiSpaceId = wikiSpace.Id,
                RepositoryVersionId = repoVersion.Id,
                VersionNo = versionNo,
                GenerationMode = task.ForceRefresh ? "rebuild" : "latest",
                GenerationProfile = generationProfile,
                Status = "ready",
                PageCount = 0,
                TocDepth = 1,
                SummaryMarkdown = $"由任务 {task.Id} 生成",
                StructureJson = structureJson,
                CreatedByTaskId = task.Id,
                CompletedAt = DateTime.UtcNow
            };
            wikiVersion = await wikiVersionRepo.AddAsync(wikiVersion);

            // 5. 更新 WikiPages 关联到 WikiVersion
            var pages = await pageRepo.GetByWikiIdAsync(wikiId);
            var pageCount = 0;
            foreach (var page in pages.Where(p => p.WikiVersionId == null))
            {
                page.WikiVersionId = wikiVersion.Id;
                await pageRepo.UpdateAsync(page);
                pageCount++;
            }

            // 更新页数
            wikiVersion.PageCount = pageCount;
            await wikiVersionRepo.UpdateAsync(wikiVersion);

            // 6. 设置发布态
            if (wikiSpace.PublishedWikiVersionId == null)
            {
                wikiSpace.PublishedWikiVersionId = wikiVersion.Id;
                wikiVersion.Status = "published";
                await spaceRepo.UpdateAsync(wikiSpace);
                await wikiVersionRepo.UpdateAsync(wikiVersion);
            }

            // 7. 回写 TaskRecord 的版本关联
            task.ResolvedRepositoryVersionId = repoVersion.Id;
            task.ResultWikiVersionId = wikiVersion.Id;

            using var taskScope = _scopeFactory.CreateScope();
            var taskRepo = taskScope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var t = await taskRepo.GetByIdAsync(task.Id);
            if (t is not null)
            {
                t.ResolvedRepositoryVersionId = repoVersion.Id;
                t.ResultWikiVersionId = wikiVersion.Id;
                await taskRepo.UpdateStatusAsync(t.Id, t.Status, t.ProgressPercent, t.ProgressMessage, t.ErrorMessage);
            }

            _logger.LogInformation("V2 版本记录已创建 RepoVersionId={RvId} WikiVersionId={WvId} Pages={Count}",
                repoVersion.Id, wikiVersion.Id, pageCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V2 版本记录创建失败（非致命）TaskId={TaskId}", task.Id);
        }
    }

    private async Task SaveWikiPageRelationsAsync(Guid wikiVersionId, WikiStructureDto structure,
        Dictionary<string, Guid> pageIdMapping)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var relationRepo = scope.ServiceProvider.GetRequiredService<IWikiPageRelationRepository>();

            await relationRepo.DeleteByVersionIdAsync(wikiVersionId);

            var newRelations = new List<WikiPageRelation>();

            foreach (var page in structure.Pages)
            {
                if (!pageIdMapping.TryGetValue(page.Id, out var sourceGuid)) continue;

                // 关联页面关系
                if (page.RelatedPages is not null)
                {
                    foreach (var relatedId in page.RelatedPages)
                    {
                        if (!pageIdMapping.TryGetValue(relatedId, out var targetGuid)) continue;

                        newRelations.Add(new WikiPageRelation
                        {
                            WikiVersionId = wikiVersionId,
                            SourcePageId = sourceGuid,
                            TargetPageId = targetGuid,
                            RelationType = "related_to",
                            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                source_page_ref = page.Id,
                                target_page_ref = relatedId
                            })
                        });
                    }
                }

                // 父页面关系
                if (!string.IsNullOrWhiteSpace(page.ParentId) && pageIdMapping.TryGetValue(page.ParentId, out var parentGuid))
                {
                    newRelations.Add(new WikiPageRelation
                    {
                        WikiVersionId = wikiVersionId,
                        SourcePageId = sourceGuid,
                        TargetPageId = parentGuid,
                        RelationType = "parent",
                        MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            source_page_ref = page.Id,
                            parent_page_ref = page.ParentId
                        })
                    });
                }
            }

            if (newRelations.Count > 0)
            {
                await relationRepo.AddRangeAsync(newRelations);
            }

            _logger.LogInformation("页面关系已保存 VersionId={VersionId} Relations={Count}", wikiVersionId, newRelations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存页面关系失败（非致命）VersionId={VersionId}", wikiVersionId);
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
            _logger.LogWarning(ex, "XML 解析失败，尝试 Regex 兜底提取");
            var regexStructure = ParseWikiStructureWithRegex(response, comprehensive);
            if (regexStructure.Pages.Count > 0)
                return regexStructure;

            _logger.LogWarning("Regex 提取也失败，使用硬编码兜底");
            return BuildFallbackStructure(response);
        }
    }

    private static string SanitizeXml(string xml) =>
        Regex.Replace(xml, "&(?![a-zA-Z]+;|#\\d+;|#x[0-9a-fA-F]+;)", "&amp;");

    /// <summary>
    /// Regex 兜底：当 XML 解析失败时，用正则直接提取 page/section 结构，
    /// 比硬编码单页兜底更可靠，能恢复 LLM 生成的大部分页面结构。
    /// </summary>
    private static WikiStructureDto ParseWikiStructureWithRegex(string response, bool comprehensive)
    {
        try
        {
            var cleaned = WikiMarkdownNormalizer.Normalize(response);
            var blockMatch = Regex.Match(cleaned, "<wiki_structure>(.*?)</wiki_structure>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!blockMatch.Success) return new WikiStructureDto { Pages = new() };

            var block = blockMatch.Groups[1].Value;

            var titleMatch = Regex.Match(block, "<title>\\s*(.*?)\\s*</title>", RegexOptions.Singleline);
            var descMatch = Regex.Match(block, "<description>\\s*(.*?)\\s*</description>", RegexOptions.Singleline);

            // 提取 <page> 元素
            var pages = new List<WikiPageDto>();
            foreach (Match pm in Regex.Matches(block, @"<page\s[^>]*id\s*=\s*""([^""]+)""[^>]*>(.*?)</page>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var id = pm.Groups[1].Value;
                var inner = pm.Groups[2].Value;
                var page = new WikiPageDto
                {
                    Id = id,
                    Title = Regex.Match(inner, @"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Description = Regex.Match(inner, @"<description>\s*(.*?)\s*</description>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Importance = NormalizeImportance(Regex.Match(inner, @"<importance>\s*(.*?)\s*</importance>", RegexOptions.Singleline).Groups[1].Value),
                    FilePaths = Regex.Matches(inner, @"<file_path>\s*(.*?)\s*</file_path>", RegexOptions.Singleline)
                        .Select(fm => fm.Groups[1].Value.Trim()).Where(f => !string.IsNullOrWhiteSpace(f)).ToList(),
                    RelatedPages = Regex.Matches(inner, @"<related>\s*(.*?)\s*</related>", RegexOptions.Singleline)
                        .Select(rm => rm.Groups[1].Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList(),
                    ParentId = Regex.Match(inner, @"<parent_section>\s*(.*?)\s*</parent_section>", RegexOptions.Singleline).Groups[1].Value.Trim()
                };
                if (!string.IsNullOrWhiteSpace(page.Id) && !string.IsNullOrWhiteSpace(page.Title))
                    pages.Add(page);
            }

            // 提取 <section> 元素
            var sections = new List<WikiSectionDto>();
            foreach (Match sm in Regex.Matches(block, @"<section\s[^>]*id\s*=\s*""([^""]+)""[^>]*>(.*?)</section>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var secId = sm.Groups[1].Value;
                var secInner = sm.Groups[2].Value;
                var section = new WikiSectionDto
                {
                    Id = secId,
                    Title = Regex.Match(secInner, @"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Pages = Regex.Matches(secInner, @"<page_ref>\s*(.*?)\s*</(?:page_ref|[^>]+)>", RegexOptions.Singleline)
                        .Select(m => m.Groups[1].Value.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                    Subsections = Regex.Matches(secInner, @"<section_ref>\s*(.*?)\s*</(?:section_ref|[^>]+)>", RegexOptions.Singleline)
                        .Select(m => m.Groups[1].Value.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
                };
                if (!string.IsNullOrWhiteSpace(section.Id))
                    sections.Add(section);
            }

            if (pages.Count == 0) return new WikiStructureDto { Pages = new() };

            if (sections.Count == 0)
            {
                sections = new List<WikiSectionDto>
                {
                    new() { Id = "default-section", Title = "Pages", Pages = pages.Select(p => p.Id).ToList() }
                };
            }

            return new WikiStructureDto
            {
                Id = "wiki",
                Title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Repository Wiki",
                Description = descMatch.Success ? descMatch.Groups[1].Value.Trim() : "",
                Pages = pages,
                Sections = sections,
                RootSections = sections.Select(s => s.Id).ToList()
            };
        }
        catch
        {
            return new WikiStructureDto { Pages = new() };
        }
    }

    private static string RepairXmlIssues(string xml)
    {
        // 修复常见的 LLM 输出 XML 错误
        xml = Regex.Replace(xml,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</page>)",
            "$1</parent_section>$2", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</related_pages>)",
            "$1</parent_section>$2", RegexOptions.IgnoreCase);
        // <page_ref>page-6</page-6> → <page_ref>page-6</page_ref>
        xml = Regex.Replace(xml,
            @"(<page_ref>[^<]+)</[^>]+>",
            "$1</page_ref>", RegexOptions.IgnoreCase);
        // <section_ref>section-1</section-1> → <section_ref>section-1</section_ref>
        xml = Regex.Replace(xml,
            @"(<section_ref>[^<]+)</[^>]+>",
            "$1</section_ref>", RegexOptions.IgnoreCase);
        // <related>page-2</page-2> → <related>page-2</related>
        xml = Regex.Replace(xml,
            @"(<related>[^<]+)</[^>]+>",
            "$1</related>", RegexOptions.IgnoreCase);
        return xml;
    }

    private static string NormalizeImportance(string? value) =>
        value?.Trim().ToLowerInvariant() switch { "high" => "high", "low" => "low", _ => "medium" };

    /// <summary>
    /// 从已克隆的仓库中读取页面关联的源文件内容，限制每个文件最大 12KB。
    /// </summary>
    private static string ReadPageFiles(string repoPath, List<string> filePaths)
    {
        if (filePaths is null || filePaths.Count == 0) return "（无关联源文件）";

        var parts = new List<string>();
        const int maxPerFile = 12288;

        foreach (var relativePath in filePaths)
        {
            try
            {
                var fullPath = Path.Combine(repoPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) continue;

                var content = File.ReadAllText(fullPath);
                if (content.Length > maxPerFile)
                    content = content[..maxPerFile] + $"\n... (文件过长，截断至 {maxPerFile} 字符，原 {content.Length} 字符)";

                parts.Add($"### {relativePath}\n```\n{content}\n```");
            }
            catch
            {
                // skip unreadable files
            }
        }

        return parts.Count == 0 ? "（无法读取关联源文件）" : string.Join("\n\n", parts);
    }

    private WikiStructureDto BuildFallbackStructure(string response)
    {
        return new WikiStructureDto
        {
            Id = "wiki",
            Title = "Repository Wiki",
            Description = $"LLM 返回内容未能解析为有效的 Wiki 结构。原始响应：{response}",
            Pages = new List<WikiPageDto>
            {
                new() { Id = "overview", Title = "仓库概览", Description = response.Length > 500 ? response[..500] : response, Importance = "high" }
            },
            Sections = new List<WikiSectionDto>
            {
                new() { Id = "default-section", Title = "仓库概览", Pages = new List<string> { "overview" } }
            },
            RootSections = new List<string> { "default-section" }
        };
    }
}
