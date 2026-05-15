using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    private const int PageBatchSize = 5;
    private static readonly System.Text.Json.JsonSerializerOptions ArtifactJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskLlmService _taskLlm;
    private readonly TaskPromptService _taskPrompt;
    private readonly WikiGenerationParserService _wikiParser;
    private readonly WikiGlobalConvergenceService _wikiConvergence;
    private readonly WikiRenderPostProcessor _wikiRenderPostProcessor;
    private readonly RepositoryAccessService _repoAccess;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<WikiTaskService> _logger;

    public WikiTaskService(
        IServiceScopeFactory scopeFactory,
        TaskLlmService taskLlm,
        TaskPromptService taskPrompt,
        WikiGenerationParserService wikiParser,
        WikiGlobalConvergenceService wikiConvergence,
        WikiRenderPostProcessor wikiRenderPostProcessor,
        RepositoryAccessService repoAccess,
        IHostApplicationLifetime appLifetime,
        ILogger<WikiTaskService> logger)
    {
        _scopeFactory = scopeFactory;
        _taskLlm = taskLlm;
        _taskPrompt = taskPrompt;
        _wikiParser = wikiParser;
        _wikiConvergence = wikiConvergence;
        _wikiRenderPostProcessor = wikiRenderPostProcessor;
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
            using var execScope = _scopeFactory.CreateScope();
            var repoAccess = execScope.ServiceProvider.GetRequiredService<RepositoryAccessService>();
            var taskRepo = execScope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var artifactRepo = execScope.ServiceProvider.GetRequiredService<ITaskArtifactRepository>();
            var executionRepo = execScope.ServiceProvider.GetRequiredService<IWikiTaskExecutionRepository>();
            var codeEmbedService = execScope.ServiceProvider.GetRequiredService<ICodeEmbeddingService>();
            var wikiEmbedService = execScope.ServiceProvider.GetRequiredService<IWikiEmbeddingService>();
            var executingTask = await taskRepo.GetByIdAsync(task.Id)
                ?? throw new InvalidOperationException($"任务不存在：{task.Id}");
            var effectiveProvider = string.IsNullOrWhiteSpace(provider) ? "ollama" : provider;

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "repository_preparation",
                "running",
                5,
                "正在准备仓库...",
                execToken,
                incrementAttempt: true,
                clearError: true);

            var repoPath = await repoAccess.PrepareRepositoryAsync(repoUrl, repoType, token, execToken);
            var localStructure = repoAccess.GetLocalStructure(repoPath);
            var fileCount = localStructure.FileTree.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "repository_preparation",
                "completed",
                15,
                $"仓库准备完成，共 {fileCount} 个文件",
                execToken,
                markStageAsSuccessful: true);

            _logger.LogInformation("仓库准备完成 TaskId={TaskId} Files={Count} Path={Path}", task.Id, fileCount, repoPath);

            var langDisplay = language == "zh" ? "中文" : "English";
            var (execOwner, execRepo) = repoAccess.FindSource(repoType, repoUrl).ParseOwnerRepo(repoUrl);
            var structureRecovery = await TryLoadPlanningArtifactAsync(artifactRepo, executingTask.Id, execToken);
            string structureRawResponse;
            string structureJson;
            WikiStructureDto wikiStructure;
            if (structureRecovery.HasValue)
            {
                structureRawResponse = structureRecovery.Value.StructureResponse;
                wikiStructure = structureRecovery.Value.Structure;
                structureJson = _wikiParser.SerializeStructure(wikiStructure);

                await MarkTaskStageAsync(
                    taskRepo,
                    executingTask,
                    "structure_planning",
                    "completed",
                    35,
                    $"已从工件恢复结构规划，共 {wikiStructure.Pages.Count} 个页面",
                    execToken,
                    markStageAsSuccessful: true);
            }
            else
            {
                var structurePrompt = _taskPrompt.BuildWikiStructurePrompt(
                    execOwner, execRepo, localStructure.FileTree, localStructure.Readme, langDisplay, comprehensive,
                    generationProfile);

                await MarkTaskStageAsync(
                    taskRepo,
                    executingTask,
                    "structure_planning",
                    "running",
                    20,
                    "正在生成 Wiki 结构...",
                    execToken);

                var structureSw = Stopwatch.StartNew();
                structureRawResponse = await _taskLlm.GenerateTextAsync(effectiveProvider, model, customModel, structurePrompt, execToken);
                await LogLlmCallAsync(task.Id, 0, "structure_generation", effectiveProvider, model ?? customModel,
                    structurePrompt, structureRawResponse, (int)structureSw.ElapsedMilliseconds, false);

                wikiStructure = _wikiParser.ParseStructure(structureRawResponse, comprehensive);
                structureJson = _wikiParser.SerializeStructure(wikiStructure);
                await UpsertTaskArtifactAsync(
                    artifactRepo,
                    taskRepo,
                    executingTask,
                    "planning_artifact",
                    "plan",
                    "structure_planning",
                    0,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        request_id = requestId,
                        repository_path = repoPath,
                        structure_raw_response = structureRawResponse,
                        structure_format = "json-dto",
                        structure_json = structureJson,
                        structure = wikiStructure
                    }, ArtifactJsonOptions),
                    $"结构规划完成，共 {wikiStructure.Pages.Count} 个页面",
                    execToken);

                await MarkTaskStageAsync(
                    taskRepo,
                    executingTask,
                    "structure_planning",
                    "completed",
                    35,
                    $"Wiki 结构生成完成，共 {wikiStructure.Pages.Count} 个页面",
                    execToken,
                    markStageAsSuccessful: true);
            }

            var completedBatchKeys = await RestoreCompletedPageBatchesAsync(artifactRepo, executingTask.Id, wikiStructure, execToken);
            var totalPages = wikiStructure.Pages.Count;
            var totalBatchCount = Math.Max(1, (int)Math.Ceiling(totalPages / (double)PageBatchSize));

            for (var batchIndex = 0; batchIndex < totalBatchCount; batchIndex++)
            {
                execToken.ThrowIfCancellationRequested();

                var batchKey = BuildBatchArtifactKey(batchIndex);
                var batchPages = wikiStructure.Pages
                    .Skip(batchIndex * PageBatchSize)
                    .Take(PageBatchSize)
                    .ToList();

                var percent = 35 + (int)(40.0 * (batchIndex + 1) / totalBatchCount);
                if (completedBatchKeys.Contains(batchKey))
                {
                    await MarkTaskStageAsync(
                        taskRepo,
                        executingTask,
                        "page_generation",
                        "running",
                        percent,
                        $"已从工件恢复页面批次 {batchIndex + 1}/{totalBatchCount}",
                        execToken);
                    continue;
                }

                await MarkTaskStageAsync(
                    taskRepo,
                    executingTask,
                    "page_generation",
                    "running",
                    percent,
                    $"正在生成页面批次 {batchIndex + 1}/{totalBatchCount}",
                    execToken);

                foreach (var page in batchPages)
                {
                    execToken.ThrowIfCancellationRequested();

                    var fileContents = ReadPageFiles(repoPath, page.FilePaths);
                    var pagePrompt = _taskPrompt.BuildWikiPagePrompt(
                        page, wikiStructure.Pages, execOwner, execRepo, repoType, repoUrl, langDisplay, fileContents);

                    var pageSw = Stopwatch.StartNew();
                    var stepOrder = wikiStructure.Pages.FindIndex(p => p.Id == page.Id) + 1;
                    try
                    {
                        var pageResponse = await _taskLlm.GenerateTextAsync(effectiveProvider, model, customModel, pagePrompt, execToken);
                        await LogLlmCallAsync(task.Id, stepOrder, "page_generation", effectiveProvider, model ?? customModel,
                            pagePrompt, pageResponse, (int)pageSw.ElapsedMilliseconds, false);

                        var pageDraft = _wikiParser.ParsePageDraft(page, pageResponse);
                        ApplyGeneratedPageDraft(page, pageDraft);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "页面生成失败 Page={Title} TaskId={TaskId}", page.Title, task.Id);
                        ApplyGeneratedPageDraft(page, BuildFailedPageDraft(page, ex.Message));
                        await LogLlmCallAsync(task.Id, stepOrder, "page_generation", effectiveProvider, model ?? customModel,
                            pagePrompt, page.Content, (int)pageSw.ElapsedMilliseconds, true, ex.Message);
                    }
                }

                await UpsertTaskArtifactAsync(
                    artifactRepo,
                    taskRepo,
                    executingTask,
                    "page_batch_artifact",
                    batchKey,
                    "page_generation",
                    batchIndex,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        batch_index = batchIndex,
                        page_count = batchPages.Count,
                        pages = batchPages
                    }, ArtifactJsonOptions),
                    $"页面批次 {batchIndex + 1}/{totalBatchCount} 已生成",
                    execToken);
            }

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "page_generation",
                "completed",
                76,
                $"页面生成完成，共 {totalPages} 个页面",
                execToken,
                markStageAsSuccessful: true);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "quality_assurance",
                "running",
                78,
                "正在执行全局收敛...",
                execToken);

            var convergenceResult = _wikiConvergence.Converge(wikiStructure);
            wikiStructure = convergenceResult.Structure;
            await UpsertTaskArtifactAsync(
                artifactRepo,
                taskRepo,
                executingTask,
                "quality_report_artifact",
                "quality-report",
                "quality_assurance",
                0,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    generated_at = DateTime.UtcNow,
                    report = convergenceResult.QualityReport
                }, ArtifactJsonOptions),
                $"质量报告已生成，兜底页面 {convergenceResult.QualityReport.FallbackPageCount} 个",
                execToken);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "quality_assurance",
                "completed",
                80,
                "全局收敛已完成",
                execToken,
                markStageAsSuccessful: true);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "render_post_processing",
                "running",
                82,
                "正在执行渲染后处理...",
                execToken);

            var renderResult = _wikiRenderPostProcessor.PostProcess(wikiStructure);
            wikiStructure = renderResult.Structure;
            structureJson = _wikiParser.SerializeStructure(wikiStructure);

            await UpsertTaskArtifactAsync(
                artifactRepo,
                taskRepo,
                executingTask,
                "render_postprocess_artifact",
                "render-postprocess",
                "render_post_processing",
                0,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    rendered_page_count = renderResult.RenderedPageCount,
                    frontmatter_page_count = renderResult.FrontMatterPageCount,
                    outline_heading_count = renderResult.OutlineHeadingCount
                }, ArtifactJsonOptions),
                $"渲染后处理完成，共 {renderResult.RenderedPageCount} 个页面",
                execToken);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "render_post_processing",
                "completed",
                83,
                "渲染后处理完成",
                execToken,
                markStageAsSuccessful: true);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "persistence",
                "running",
                84,
                "正在写入 Wiki 主数据、页面与版本...",
                execToken);

            var persistenceResult = await PersistWikiProjectionAsync(
                executionRepo,
                executingTask,
                wikiStructure,
                structureJson,
                language,
                branch,
                generationProfile,
                execToken);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "persistence",
                "completed",
                89,
                $"主数据落库完成，WikiVersion={persistenceResult.WikiVersionId}",
                execToken,
                markStageAsSuccessful: true);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "code_embedding",
                "running",
                90,
                "正在写入代码向量...",
                execToken);

            var documents = await repoAccess.ReadRepositoryDocumentsAsync(repoPath, new(), new(), new(), new(), execToken);
            var codeChunkCount = await codeEmbedService.EmbedRepositoryAsync(persistenceResult.RepositoryVersionId, documents, execToken);
            await UpsertTaskArtifactAsync(
                artifactRepo,
                taskRepo,
                executingTask,
                "code_embedding_artifact",
                "code-embedding",
                "code_embedding",
                0,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    repository_version_id = persistenceResult.RepositoryVersionId,
                    document_count = documents.Count,
                    chunk_count = codeChunkCount
                }, ArtifactJsonOptions),
                $"代码向量写入完成，共 {codeChunkCount} 个分块",
                execToken);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "code_embedding",
                "completed",
                94,
                $"代码向量写入完成，共 {codeChunkCount} 个分块",
                execToken,
                markStageAsSuccessful: true);

            await MarkTaskStageAsync(
                taskRepo,
                executingTask,
                "wiki_embedding",
                "running",
                95,
                "正在写入 Wiki 向量...",
                execToken);

            var wikiChunkCount = await wikiEmbedService.EmbedWikiPagesAsync(persistenceResult.WikiVersionId, persistenceResult.Pages, execToken);
            await UpsertTaskArtifactAsync(
                artifactRepo,
                taskRepo,
                executingTask,
                "wiki_embedding_artifact",
                "wiki-embedding",
                "wiki_embedding",
                0,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    wiki_version_id = persistenceResult.WikiVersionId,
                    page_count = persistenceResult.Pages.Count,
                    chunk_count = wikiChunkCount
                }, ArtifactJsonOptions),
                $"Wiki 向量写入完成，共 {wikiChunkCount} 个分块",
                execToken);

            executingTask.Status = "completed";
            executingTask.CurrentStage = "completed";
            executingTask.CurrentStageStatus = "completed";
            executingTask.LastSuccessfulStage = "wiki_embedding";
            executingTask.ProgressPercent = 100;
            executingTask.ProgressMessage = $"Wiki 生成完成，共 {totalPages} 个页面";
            executingTask.CompletedAt = DateTime.UtcNow;
            executingTask.ResultJson = BuildResultSummaryJson(
                wikiStructure,
                persistenceResult.RepositoryVersionId,
                persistenceResult.WikiVersionId,
                codeChunkCount,
                wikiChunkCount);
            await taskRepo.UpdateAsync(executingTask);

            _logger.LogInformation("Wiki 生成完成 TaskId={TaskId} Pages={Count} Elapsed={Ms}ms",
                task.Id, totalPages, totalStopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            await MarkTerminalStateAsync(task.Id, "cancelled", "任务已取消", "cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki 生成失败 TaskId={TaskId}", task.Id);
            await MarkTerminalStateAsync(task.Id, "failed", ex.Message, "failed");
        }
    }

    /// <summary>
    /// 持久化任务阶段状态。
    /// 该方法统一维护整体状态、阶段状态、尝试次数与最近成功阶段。
    /// </summary>
    private static async Task MarkTaskStageAsync(
        ITaskRepository taskRepo,
        TaskRecord task,
        string stageName,
        string stageStatus,
        int progressPercent,
        string progressMessage,
        CancellationToken cancellationToken,
        bool incrementAttempt = false,
        bool clearError = false,
        bool markStageAsSuccessful = false)
    {
        task.Status = stageStatus == "completed" && stageName == "completed" ? "completed" : "running";
        task.CurrentStage = stageName;
        task.CurrentStageStatus = stageStatus;
        task.ProgressPercent = progressPercent;
        task.ProgressMessage = progressMessage;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.StartedAt is null)
            task.StartedAt = DateTime.UtcNow;

        if (incrementAttempt)
            task.AttemptCount++;

        if (clearError)
            task.ErrorMessage = null;

        if (markStageAsSuccessful)
            task.LastSuccessfulStage = stageName;

        await taskRepo.UpdateAsync(task);
    }

    /// <summary>
    /// 终结任务状态。
    /// 该方法用于失败或取消场景，确保整体状态、阶段状态与完成时间保持一致。
    /// </summary>
    private async Task MarkTerminalStateAsync(Guid taskId, string status, string errorMessage, string stageStatus)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var task = await taskRepo.GetByIdAsync(taskId);
        if (task is null)
            return;

        task.Status = status;
        task.CurrentStageStatus = stageStatus;
        task.ProgressMessage = status == "cancelled" ? "任务已取消" : $"任务失败：{errorMessage}";
        task.ErrorMessage = errorMessage;
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await taskRepo.UpdateAsync(task);
    }

    /// <summary>
    /// 尝试读取结构规划工件。
    /// 读取成功后即可跳过结构规划阶段并从该恢复点继续执行。
    /// </summary>
    private static async Task<(string StructureResponse, WikiStructureDto Structure)?> TryLoadPlanningArtifactAsync(
        ITaskArtifactRepository artifactRepo,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var artifact = await artifactRepo.GetByTypeAndKeyAsync(taskId, "planning_artifact", "plan");

        if (artifact is null || artifact.Status != "completed")
            return null;

        using var document = System.Text.Json.JsonDocument.Parse(artifact.PayloadJson);
        if (!document.RootElement.TryGetProperty("structure", out var structureElement))
            return null;

        var structure = System.Text.Json.JsonSerializer.Deserialize<WikiStructureDto>(
            structureElement.GetRawText(),
            ArtifactJsonOptions);
        if (structure is null)
            return null;

        var structureResponse = document.RootElement.TryGetProperty("structure_raw_response", out var rawResponseElement)
            ? rawResponseElement.GetString() ?? string.Empty
            : document.RootElement.TryGetProperty("structure_response", out var responseElement)
                ? responseElement.GetString() ?? string.Empty
            : string.Empty;

        return (structureResponse, structure);
    }

    /// <summary>
    /// 从已完成的页面批次工件恢复页面内容。
    /// 返回值为已恢复的批次键集合，调用方可据此跳过重复生成。
    /// </summary>
    private static async Task<HashSet<string>> RestoreCompletedPageBatchesAsync(
        ITaskArtifactRepository artifactRepo,
        Guid taskId,
        WikiStructureDto wikiStructure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifacts = (await artifactRepo.GetByTypeAsync(taskId, "page_batch_artifact"))
            .Where(a => a.Status == "completed")
            .OrderBy(a => a.Sequence)
            .ToList();

        var pageLookup = wikiStructure.Pages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var restored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artifact in artifacts)
        {
            using var document = System.Text.Json.JsonDocument.Parse(artifact.PayloadJson);
            if (!document.RootElement.TryGetProperty("pages", out var pagesElement))
                continue;

            var pages = System.Text.Json.JsonSerializer.Deserialize<List<WikiPageDto>>(
                pagesElement.GetRawText(),
                ArtifactJsonOptions);
            if (pages is null || pages.Count == 0)
                continue;

            foreach (var page in pages)
            {
                if (!pageLookup.TryGetValue(page.Id, out var target))
                    continue;

                ApplyGeneratedPageDraft(target, page);
            }

            restored.Add(artifact.ArtifactKey);
        }

        return restored;
    }

    /// <summary>
    /// 将工件结果幂等写入数据库，并同步回写任务恢复锚点。
    /// </summary>
    private static async Task<TaskArtifact> UpsertTaskArtifactAsync(
        ITaskArtifactRepository artifactRepo,
        ITaskRepository taskRepo,
        TaskRecord task,
        string artifactType,
        string artifactKey,
        string stageName,
        int sequence,
        string payloadJson,
        string summary,
        CancellationToken cancellationToken,
        string status = "completed",
        string? errorMessage = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = await artifactRepo.UpsertAsync(new TaskArtifact
        {
            TaskId = task.Id,
            ArtifactType = artifactType,
            ArtifactKey = artifactKey,
            StageName = stageName,
            Status = status,
            Sequence = sequence,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant(),
            Summary = summary,
            PayloadJson = payloadJson,
            ErrorMessage = errorMessage
        });

        task.LastArtifactId = artifact.Id;
        task.LastSuccessfulStage = status == "completed" ? stageName : task.LastSuccessfulStage;
        task.UpdatedAt = DateTime.UtcNow;
        await taskRepo.UpdateAsync(task);
        return artifact;
    }

    /// <summary>
    /// 持久化 Wiki 主数据、版本数据、页面数据、关系数据与渲染快照。
    /// 该方法在同一数据库事务中完成主链路落库，确保完成态与真实结果一致。
    /// </summary>
    private static async Task<(Guid WikiId, Guid RepositoryVersionId, Guid WikiVersionId, List<WikiPage> Pages)> PersistWikiProjectionAsync(
        IWikiTaskExecutionRepository executionRepository,
        TaskRecord task,
        WikiStructureDto structure,
        string structureJson,
        string language,
        string branch,
        string generationProfile,
        CancellationToken cancellationToken)
    {
        return await executionRepository.PersistWikiProjectionAsync(
            task,
            structure,
            structureJson,
            language,
            branch,
            generationProfile,
            cancellationToken);
    }

    /// <summary>
    /// 生成页面批次工件键。
    /// </summary>
    private static string BuildBatchArtifactKey(int batchIndex) => $"batch-{batchIndex:D4}";

    /// <summary>
    /// 生成任务结果摘要 JSON。
    /// </summary>
    private static string BuildResultSummaryJson(
        WikiStructureDto structure,
        Guid repositoryVersionId,
        Guid wikiVersionId,
        int? codeChunkCount,
        int? wikiChunkCount)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            structure.Title,
            structure.Description,
            page_count = structure.Pages.Count,
            repository_version_id = repositoryVersionId,
            wiki_version_id = wikiVersionId,
            code_chunk_count = codeChunkCount,
            wiki_chunk_count = wikiChunkCount,
            pages = structure.Pages.Select(p => new
            {
                p.Id,
                p.Title,
                p.Importance,
                content_length = p.Content.Length
            })
        }, ArtifactJsonOptions);
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

    /// <summary>
    /// 将单页草案结果合并回结构规划中的页面对象。
    /// </summary>
    private static void ApplyGeneratedPageDraft(WikiPageDto target, WikiPageDto draft)
    {
        target.Title = draft.Title;
        target.Description = draft.Description;
        target.Content = draft.Content;
        target.NavTitle = draft.NavTitle;
        target.PageType = draft.PageType;
        target.FilePaths = draft.FilePaths;
        target.Importance = draft.Importance;
        target.RelatedPages = draft.RelatedPages;
        target.PrerequisitePages = draft.PrerequisitePages;
        target.ParentId = draft.ParentId;
        target.IsSection = draft.IsSection;
        target.Children = draft.Children;
        target.FrontMatter = draft.FrontMatter;
        target.Outline = draft.Outline;
        target.SourceCoverage = draft.SourceCoverage;
        target.Warnings = draft.Warnings;
        target.IsFallbackDraft = draft.IsFallbackDraft;
    }

    /// <summary>
    /// 构建生成失败时的页面兜底草案。
    /// </summary>
    private static WikiPageDto BuildFailedPageDraft(WikiPageDto page, string errorMessage)
    {
        return new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Description = page.Description,
            Content = $"## 生成异常\n\n> 页面生成失败：{errorMessage}\n\n## 页面说明\n\n{page.Description}",
            NavTitle = string.IsNullOrWhiteSpace(page.NavTitle) ? page.Title : page.NavTitle,
            PageType = string.IsNullOrWhiteSpace(page.PageType) ? "article" : page.PageType,
            FilePaths = page.FilePaths,
            Importance = page.Importance,
            RelatedPages = page.RelatedPages,
            PrerequisitePages = page.PrerequisitePages,
            ParentId = page.ParentId,
            IsSection = page.IsSection,
            Children = page.Children,
            FrontMatter = new WikiPageFrontMatterDto
            {
                Summary = page.Description,
                Description = page.Description,
                SourceFiles = page.FilePaths
            },
            Outline = new(),
            SourceCoverage = new WikiPageSourceCoverageDto
            {
                PrimaryFiles = page.FilePaths
            },
            Warnings = new() { $"页面生成失败：{errorMessage}" },
            IsFallbackDraft = true
        };
    }

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
}
