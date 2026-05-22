using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Repository;
using Heimdall.Core.Services.Search;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Search;
using Heimdall.Infrastructure.Services;
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
    private readonly CodeStructureIndexService _codeIndexService;
    private readonly IHybridSearchService _hybridSearch;
    private readonly RepositoryAccessService _repoAccess;
    private readonly Infrastructure.Configuration.HeimdallConfigService _configService;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IStructuredLogger _structuredLogger;
    private readonly ILogger<WikiTaskService> _logger;

    public WikiTaskService(
        IServiceScopeFactory scopeFactory,
        TaskLlmService taskLlm,
        TaskPromptService taskPrompt,
        WikiGenerationParserService wikiParser,
        WikiGlobalConvergenceService wikiConvergence,
        WikiRenderPostProcessor wikiRenderPostProcessor,
        CodeStructureIndexService codeIndexService,
        IHybridSearchService hybridSearch,
        RepositoryAccessService repoAccess,
        Infrastructure.Configuration.HeimdallConfigService configService,
        IHostApplicationLifetime appLifetime,
        IStructuredLogger structuredLogger,
        ILogger<WikiTaskService> logger)
    {
        _scopeFactory = scopeFactory;
        _taskLlm = taskLlm;
        _taskPrompt = taskPrompt;
        _wikiParser = wikiParser;
        _wikiConvergence = wikiConvergence;
        _wikiRenderPostProcessor = wikiRenderPostProcessor;
        _codeIndexService = codeIndexService;
        _hybridSearch = hybridSearch;
        _repoAccess = repoAccess;
        _configService = configService;
        _appLifetime = appLifetime;
        _structuredLogger = structuredLogger;
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

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(100));
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
            _structuredLogger.LogTaskProgress(task.Id, "仓库准备", null, null, $"共 {fileCount} 个文件");

            // ── 仓库文档收集 ──
            var repositoryDocs = CollectRepositoryDocuments(repoPath);
            if (repositoryDocs.Count > 0)
            {
                _logger.LogInformation("仓库文档收集完成 TaskId={TaskId} DocCount={Count}", task.Id, repositoryDocs.Count);
                _structuredLogger.LogTaskProgress(task.Id, "文档收集", null, null, $"已收集 {repositoryDocs.Count} 个仓库文档");
            }

            var langDisplay = language == "zh" ? "中文" : "English";

            // ── V6: 本地代码索引阶段（无 LLM 摘要）──
            var codeIndexResult = _codeIndexService.IndexRepository(repoPath);
            var codeAnalysisRecovery = await TryLoadArtifactByTypeAsync(artifactRepo, executingTask.Id, "code_index_artifact", execToken);

            if (codeAnalysisRecovery is not null)
            {
                _logger.LogInformation("从工件恢复代码索引结果 TaskId={TaskId}", task.Id);
                await MarkTaskStageAsync(taskRepo, executingTask, "code_indexing", "completed", 25,
                    "已从工件恢复代码索引结果", execToken, markStageAsSuccessful: true);
            }
            else
            {
                await MarkTaskStageAsync(taskRepo, executingTask, "code_indexing", "running", 17,
                    "正在执行代码结构索引...", execToken);

                codeIndexResult = _codeIndexService.IndexRepository(repoPath);

                await MarkTaskStageAsync(taskRepo, executingTask, "code_indexing", "running", 22,
                    $"结构索引完成，共 {codeIndexResult.SourceFileCount} 个源文件，{codeIndexResult.ModuleNames.Count} 个模块",
                    execToken);

                // 持久化索引结果（不再包含 LLM 摘要）
                await UpsertTaskArtifactAsync(artifactRepo, taskRepo, executingTask,
                    "code_index_artifact", "analysis", "code_indexing", 0,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        project_type = codeIndexResult.ProjectType,
                        tech_stack = codeIndexResult.TechStack,
                        total_files = codeIndexResult.TotalFileCount,
                        source_files = codeIndexResult.SourceFileCount,
                        module_count = codeIndexResult.ModuleNames.Count,
                        module_names = codeIndexResult.ModuleNames,
                        entry_points = codeIndexResult.EntryPointFiles
                    }, ArtifactJsonOptions),
                    $"代码索引完成：{codeIndexResult.ModuleNames.Count} 个模块",
                    execToken);

                await MarkTaskStageAsync(taskRepo, executingTask, "code_indexing", "completed", 28,
                    $"代码索引完成：{codeIndexResult.ProjectType} / {codeIndexResult.TechStack}，{codeIndexResult.ModuleNames.Count} 个模块",
                    execToken, markStageAsSuccessful: true);
            }

            // 构建混合搜索索引（BM25 + 向量嵌入），供页面生成时检索真实代码
            var searchIndexKey = $"repo-{executingTask.Id}";
            await BuildSearchIndexAsync(repoPath, codeIndexResult, searchIndexKey, execToken);

            // 深度代码理解阶段（调用图 + 依赖拓扑 + 设计模式 + LLM 架构洞察）
            CodeUnderstandingResult? codeUnderstanding = null;
            var codeUnderstandingRecovery = await TryLoadArtifactByTypeAsync(
                artifactRepo, executingTask.Id, "code_understanding", execToken);
            if (codeUnderstandingRecovery is not null)
            {
                try
                {
                    codeUnderstanding = System.Text.Json.JsonSerializer.Deserialize<CodeUnderstandingResult>(
                        codeUnderstandingRecovery, ArtifactJsonOptions);
                    _logger.LogInformation("从工件恢复深度代码理解 TaskId={TaskId}", task.Id);
                }
                catch { /* 恢复失败，重新执行 */ }
            }

            if (codeUnderstanding is null)
            {
                await MarkTaskStageAsync(taskRepo, executingTask, "code_understanding", "running", 28,
                    "正在执行深度代码理解分析...", execToken);

                var codeUnderstandingService = execScope.ServiceProvider
                    .GetRequiredService<ICodeUnderstandingService>();
                codeUnderstanding = await codeUnderstandingService.AnalyzeAsync(
                    Guid.Empty,
                    repoPath, effectiveProvider, model ?? customModel, execToken);

                await UpsertTaskArtifactAsync(artifactRepo, taskRepo, executingTask,
                    "code_understanding", "analysis", "code_understanding", 0,
                    System.Text.Json.JsonSerializer.Serialize(codeUnderstanding, ArtifactJsonOptions),
                    $"深度代码理解完成：{codeUnderstanding.CallGraph.NodeCount} 方法节点，{codeUnderstanding.DesignPatterns.Count} 设计模式",
                    execToken);

                await MarkTaskStageAsync(taskRepo, executingTask, "code_understanding", "completed", 32,
                    $"深度代码理解完成：{codeUnderstanding.CallGraph.NodeCount} 节点，{codeUnderstanding.DependencyTopology.Modules.Count} 模块",
                    execToken, markStageAsSuccessful: true);
            }

            _logger.LogInformation(
                "[Wiki] 深度理解结果 TaskId={TaskId} CallGraphNodes={Nodes} CallGraphEdges={Edges} MaxDepth={Depth} Modules={Modules} Patterns={Patterns}",
                task.Id,
                codeUnderstanding.CallGraph.NodeCount,
                codeUnderstanding.CallGraph.Edges.Count,
                codeUnderstanding.CallGraph.MaxDepth,
                codeUnderstanding.DependencyTopology.Modules.Count,
                codeUnderstanding.DesignPatterns.Count);

            var callGraphDepth = codeUnderstanding.CallGraph.MaxDepth;
            var patternCount = codeUnderstanding.DesignPatterns.Count;
            var recommendedPageCount = CodeStructureIndexService.CalculateRecommendedPageCount(
                codeIndexResult.ModuleNames.Count,
                codeIndexResult.EntryPointFiles.Count,
                patternCount,
                callGraphDepth);
            var maxDepthLevel = CodeStructureIndexService.CalculateMaxDepth(codeIndexResult.TotalFileCount);
            _logger.LogInformation(
                "[Wiki] 页面规划：推荐 {PageCount} 页，最大深度 {MaxDepth} 层 (files={Files}, modules={Modules}, patterns={Patterns}, graphDepth={GraphDepth})",
                recommendedPageCount, maxDepthLevel, codeIndexResult.TotalFileCount,
                codeIndexResult.ModuleNames.Count, patternCount, callGraphDepth);

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
                var repositoryDocsSection = BuildRepositoryDocsSection(repositoryDocs);
                var structurePrompt = _taskPrompt.BuildWikiStructurePromptV7(
                    execOwner, execRepo, localStructure.FileTree, localStructure.Readme,
                    langDisplay, comprehensive, codeUnderstanding, generationProfile,
                    repositoryDocsSection);

                await MarkTaskStageAsync(
                    taskRepo,
                    executingTask,
                    "structure_planning",
                    "running",
                    20,
                    "正在生成 Wiki 结构...",
                    execToken);

                // 结构规划重试机制：当解析结果页面数过少时自动重试（最多 2 次）
                var minExpectedPages = comprehensive ? 6 : 3;
                var structureAttemptCount = 0;
                const int maxStructureAttempts = 3;

                do
                {
                    structureAttemptCount++;
                    var structureSw = Stopwatch.StartNew();
                    var structureResponse = await _taskLlm.GenerateWithMetricsAsync(effectiveProvider, model, customModel, structurePrompt, execToken);
                    structureRawResponse = structureResponse.Content;
                    try
                    {
                        var obs = execScope.ServiceProvider.GetRequiredService<ILlmObservabilityService>();
                        await obs.RecordCallAsync(task.Id, "structure_planning", effectiveProvider, model ?? customModel ?? "", structureResponse, execToken);
                    }
                    catch { /* 指标记录失败不影响主流程 */ }
                    await LogLlmCallAsync(task.Id, 0, "structure_generation", effectiveProvider, model ?? customModel,
                        structurePrompt, structureRawResponse, (int)structureSw.ElapsedMilliseconds, false);

                    wikiStructure = _wikiParser.ParseStructure(structureRawResponse, comprehensive);

                    if (wikiStructure.Pages.Count >= minExpectedPages)
                        break;

                    _logger.LogWarning(
                        "结构规划页面数不足 TaskId={TaskId} Attempt={Attempt} PageCount={Count} MinExpected={Min}，将重试",
                        task.Id, structureAttemptCount, wikiStructure.Pages.Count, minExpectedPages);

                    if (structureAttemptCount < maxStructureAttempts)
                    {
                        await MarkTaskStageAsync(taskRepo, executingTask, "structure_planning", "running", 22,
                            $"结构规划结果不理想（仅 {wikiStructure.Pages.Count} 页），正在重试第 {structureAttemptCount + 1} 次...",
                            execToken);
                    }
                } while (structureAttemptCount < maxStructureAttempts);

                // 即使重试后仍不足，也继续执行（使用最后一次结果）
                if (wikiStructure.Pages.Count < minExpectedPages)
                {
                    _logger.LogWarning(
                        "结构规划重试 {Attempts} 次后仍仅 {Count} 页 TaskId={TaskId}，使用当前结果继续",
                        maxStructureAttempts, wikiStructure.Pages.Count, task.Id);
                }

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

                _structuredLogger.LogTaskProgress(task.Id, "结构规划完成", null, wikiStructure.Pages.Count,
                    $"共 {wikiStructure.Pages.Count} 个页面，{wikiStructure.Sections?.Count ?? 0} 个章节");

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

            // BFS 树形拓扑序遍历——根节点 (parentId=null) 优先，逐层展开子节点
            // 确保父页面先于子页面生成，使子页面可注入父页面摘要
            wikiStructure.Pages = OrderPagesByTreeBfs(wikiStructure.Pages);

            var totalPages = wikiStructure.Pages.Count;
            var debugTruncated = false;
            var debugOriginalPageCount = totalPages;

            // ── Debug Mode 页数截断 ──
            var settingRepo = execScope.ServiceProvider.GetService<ISystemSettingRepository>();
            if (settingRepo != null)
            {
                var debugEnabled = await settingRepo.GetByKeyAsync("DebugMode.Enabled");
                if (debugEnabled?.Value == "true")
                {
                    var maxPagesSetting = await settingRepo.GetByKeyAsync("DebugMode.MaxPages");
                    var maxPages = int.TryParse(maxPagesSetting?.Value, out var mp) ? mp : 5;
                    if (totalPages > maxPages)
                    {
                        var skippedPages = wikiStructure.Pages.Skip(maxPages).Select(p => p.Title).ToList();
                        wikiStructure.Pages = wikiStructure.Pages.Take(maxPages).ToList();
                        totalPages = wikiStructure.Pages.Count;
                        debugTruncated = true;
                        _logger.LogWarning(
                            "[Wiki] 调试模式：页面已截断 TaskId={TaskId} Original={Orig} Truncated={Now} Max={Max} Skipped={Skipped}",
                            task.Id, debugOriginalPageCount, totalPages, maxPages,
                            string.Join(", ", skippedPages));
                        _structuredLogger.LogTaskProgress(task.Id, "调试模式截断", null, totalPages,
                            $"已截断页面列表：{debugOriginalPageCount} → {totalPages}（上限 {maxPages} 页）");
                    }
                }
            }

            // CodingPlan 模型使用更大的批次（减少调用次数）
            var effectiveBatchSize = PageBatchSize;
            var providerMeta = _configService.GetProviderModelMetadata(effectiveProvider, model ?? customModel ?? "");
            if (providerMeta.BillingType == BillingType.CodingPlan)
            {
                effectiveBatchSize = Math.Min(10, totalPages);
                _logger.LogInformation("[Wiki] CodingPlan 模式：使用批次大小 {BatchSize} 以减少调用次数", effectiveBatchSize);
            }

            var totalBatchCount = Math.Max(1, (int)Math.Ceiling(totalPages / (double)effectiveBatchSize));

            // V4: 跨页面上下文收集器——将已生成页面摘要注入后续页面 prompt
            var generatedPageContexts = new List<(string Title, string Summary)>();

            for (var batchIndex = 0; batchIndex < totalBatchCount; batchIndex++)
            {
                execToken.ThrowIfCancellationRequested();

                var batchKey = BuildBatchArtifactKey(batchIndex);
                var batchPages = wikiStructure.Pages
                    .Skip(batchIndex * effectiveBatchSize)
                    .Take(effectiveBatchSize)
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

                // V4: 为当前批次构建跨页面上下文（仅注入最相关的已生成页面摘要）
                var activePageContext = generatedPageContexts.Count switch
                {
                    0 => null,
                    <= 20 => string.Join("\n", generatedPageContexts.Select(c => $"- **{c.Title}**: {c.Summary}")),
                    _ => string.Join("\n", generatedPageContexts.TakeLast(10).Select(c => $"- **{c.Title}**: {c.Summary}"))
                };

                foreach (var page in batchPages)
                {
                    execToken.ThrowIfCancellationRequested();

                    // 注入父页面摘要（拓扑序确保父页面已生成）
                    string? parentContext = null;
                    if (!string.IsNullOrEmpty(page.ParentId))
                    {
                        var parentPage = wikiStructure.Pages.FirstOrDefault(p => p.Id == page.ParentId);
                        if (parentPage is not null && !string.IsNullOrWhiteSpace(parentPage.Content))
                        {
                            var parentSummary = parentPage.Content.Length > 500
                                ? parentPage.Content[..500] + "..."
                                : parentPage.Content;
                            parentContext = $"\n父页面「{parentPage.Title}」摘要：\n{parentSummary}";

                            // 祖父页面标题
                            if (!string.IsNullOrEmpty(parentPage.ParentId))
                            {
                                var grandparent = wikiStructure.Pages.FirstOrDefault(p => p.Id == parentPage.ParentId);
                                if (grandparent is not null)
                                    parentContext += $"\n祖父页面：{grandparent.Title}";
                            }
                        }
                    }

                    // 合并跨页面上下文和父页面上下文
                    var combinedContext = string.IsNullOrWhiteSpace(parentContext)
                        ? activePageContext
                        : $"{activePageContext ?? ""}\n{parentContext}";

                    // 使用混合搜索检索真实代码片段（替代旧的 ReadPageFiles）
                    var searchQuery = page.SearchKeywords?.Count > 0
                        ? string.Join(" ", page.SearchKeywords)
                        : $"{page.Title} {page.Description}";
                    var keyFiles = (page.KeyFilePaths?.Count > 0 ? page.KeyFilePaths : null)
                        ?? (page.FilePaths?.Count > 0 ? page.FilePaths : null);

                    var contextBudget = new ContextPackingService(_configService)
                        .CalculateAvailableBudget(effectiveProvider, model ?? customModel ?? "");
                    var searchResults = await _hybridSearch.SearchAsync(
                        searchIndexKey, searchQuery, keyFiles, topK: 100, maxTotalTokens: contextBudget, ct: execToken);
                    var fileContents = _hybridSearch.FormatForPrompt(searchResults);

                    // 根据页面主题有选择地注入仓库文档内容
                    var docContext = IsArchitectureOrOverviewPage(page)
                        ? BuildRepositoryDocsSection(repositoryDocs, maxChars: 3000)
                        : null;

                    var pagePrompt = _taskPrompt.BuildWikiPagePrompt(
                        page, wikiStructure.Pages, execOwner, execRepo, repoType, repoUrl, langDisplay, fileContents,
                        combinedContext, docContext);

                    var pageSw = Stopwatch.StartNew();
                    var stepOrder = wikiStructure.Pages.FindIndex(p => p.Id == page.Id) + 1;
                    try
                    {
                        // 使用带指标的调用，记录 Token 消耗
                        var pageResponseObj = await _taskLlm.GenerateWithMetricsAsync(
                            effectiveProvider, model, customModel, pagePrompt, execToken);
                        var pageResponse = pageResponseObj.Content;

                        try
                        {
                            var observability = execScope.ServiceProvider
                                .GetRequiredService<ILlmObservabilityService>();
                            await observability.RecordCallAsync(task.Id, "page_generation",
                                effectiveProvider, model ?? customModel ?? "", pageResponseObj, execToken);
                        }
                        catch { /* 指标记录失败不应中断主流程 */ }

                        try { await LogLlmCallAsync(task.Id, stepOrder, "page_generation", effectiveProvider, model ?? customModel,
                            pagePrompt, pageResponse, (int)pageSw.ElapsedMilliseconds, false); } catch { }
                        _structuredLogger.LogTaskProgress(task.Id, "页面生成", stepOrder, totalPages,
                            $"{page.Title} | {effectiveProvider}/{model ?? customModel} | {pageSw.ElapsedMilliseconds}ms | In={pageResponseObj.Usage.InputTokens} Out={pageResponseObj.Usage.OutputTokens}");

                        var pageDraft = _wikiParser.ParsePageDraft(page, pageResponse);
                        ApplyGeneratedPageDraft(page, pageDraft);
                        // V4: 收集已生成页面摘要用于跨页面上下文传递
                        var contentPreview = pageDraft.Content.Length > 200
                            ? pageDraft.Content[..200] + "..."
                            : pageDraft.Content;
                        generatedPageContexts.Add((page.Title, contentPreview));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "页面生成失败 Page={Title} TaskId={TaskId}", page.Title, task.Id);
                        _structuredLogger.LogTaskProgress(task.Id, "页面生成", stepOrder, totalPages,
                            $"失败: {page.Title} | {effectiveProvider}/{model ?? customModel}");
                        ApplyGeneratedPageDraft(page, BuildFailedPageDraft(page, ex.Message));
                        try
                        {
                            await LogLlmCallAsync(task.Id, stepOrder, "page_generation", effectiveProvider, model ?? customModel,
                                pagePrompt, page.Content, (int)pageSw.ElapsedMilliseconds, true, ex.Message);
                        }
                        catch (Exception logEx)
                        {
                            _logger.LogWarning(logEx, "LLM 调用日志保存失败 TaskId={TaskId}", task.Id);
                        }
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

            // V4: 弱页面自动重生成（最多 1 轮）
            var weakPageIds = convergenceResult.QualityReport.WeakPageIds;
            if (weakPageIds.Count > 0)
            {
                _logger.LogInformation("检测到 {Count} 个弱页面，开始自动重生成 TaskId={TaskId}",
                    weakPageIds.Count, task.Id);

                await MarkTaskStageAsync(taskRepo, executingTask, "page_regeneration", "running", 82,
                    $"正在重新生成 {weakPageIds.Count} 个弱页面...", execToken);

                var regeneratedCount = 0;
                foreach (var weakPageId in weakPageIds)
                {
                    execToken.ThrowIfCancellationRequested();
                    var weakPage = wikiStructure.Pages.FirstOrDefault(p => p.Id == weakPageId);
                    if (weakPage is null) continue;

                    var qualityScore = convergenceResult.QualityReport.PageQualityScores
                        .TryGetValue(weakPageId, out var score) ? score : 0;

                    var fileContents = ReadPageFiles(repoPath, weakPage.FilePaths);
                    var regenerationPrompt = BuildRegenerationPrompt(
                        weakPage, fileContents, qualityScore, langDisplay);

                    try
                    {
                        var regenResponse = await _taskLlm.GenerateWithMetricsAsync(
                            effectiveProvider, model, customModel, regenerationPrompt, execToken);
                        try
                        {
                            var obs = execScope.ServiceProvider.GetRequiredService<ILlmObservabilityService>();
                            await obs.RecordCallAsync(task.Id, "quality_assurance", effectiveProvider, model ?? customModel ?? "", regenResponse, execToken);
                        }
                        catch { }
                        var newDraft = _wikiParser.ParsePageDraft(weakPage, regenResponse.Content);
                        if (!string.IsNullOrWhiteSpace(newDraft.Content))
                        {
                            ApplyGeneratedPageDraft(weakPage, newDraft);
                            regeneratedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "弱页面重生成失败 Page={Title} TaskId={TaskId}",
                            weakPage.Title, task.Id);
                    }
                }

                _logger.LogInformation("弱页面重生成完成：{Count}/{Total} TaskId={TaskId}",
                    regeneratedCount, weakPageIds.Count, task.Id);
            }

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

            // V8: BM25 检索引擎不再需要预计算向量，移除 code_embedding / wiki_embedding 阶段

            executingTask.Status = "completed";
            executingTask.CurrentStage = "completed";
            executingTask.CurrentStageStatus = "completed";
            executingTask.LastSuccessfulStage = "persistence";
            executingTask.ProgressPercent = 100;
            executingTask.ProgressMessage = $"Wiki 生成完成，共 {totalPages} 个页面";
            executingTask.CompletedAt = DateTime.UtcNow;
            executingTask.ResultJson = BuildResultSummaryJson(
                wikiStructure,
                persistenceResult.RepositoryVersionId,
                persistenceResult.WikiVersionId,
                debugTruncated,
                debugOriginalPageCount);
            await taskRepo.UpdateAsync(executingTask);

            var maxDepth = wikiStructure.Pages.Any() ? wikiStructure.Pages.Max(p => p.Depth) : 0;
            _logger.LogInformation(
                "[Wiki] 生成完成 TaskId={TaskId} Pages={Pages} MaxDepth={Depth} Elapsed={Elapsed:F1}s",
                task.Id, totalPages, maxDepth, totalStopwatch.Elapsed.TotalSeconds);

            var llmCalls = 0;
            var llmInputTokens = 0;
            var llmOutputTokens = 0;
            try
            {
                using var metricScope = _scopeFactory.CreateScope();
                var obsService = metricScope.ServiceProvider
                    .GetService<ILlmObservabilityService>();
                if (obsService != null)
                {
                    var summary = await obsService.GetTaskSummaryAsync(task.Id);
                    llmCalls = summary.TotalCalls;
                    llmInputTokens = (int)summary.TotalInputTokens;
                    llmOutputTokens = (int)summary.TotalOutputTokens;
                    _logger.LogInformation(
                        "[Wiki] LLM汇总 TaskId={TaskId} Calls={Calls} InputTokens={In} OutputTokens={Out} CacheHitTokens={Cache} TotalCost≈${Cost:F4} CacheRate={Rate:P0}",
                        task.Id, summary.TotalCalls, summary.TotalInputTokens,
                        summary.TotalOutputTokens, summary.TotalCacheHitTokens,
                        summary.EstimatedCost,
                        summary.TotalInputTokens > 0
                            ? (double)summary.TotalCacheHitTokens / summary.TotalInputTokens
                            : 0.0);
                }
            }
            catch { /* 日志失败不影响主流程 */ }
            _structuredLogger.LogTaskSummary(task.Id, totalPages,
                totalStopwatch.Elapsed.TotalSeconds, llmCalls, llmInputTokens, llmOutputTokens);
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
    /// <summary>
    /// 尝试按工件类型从数据库加载已完成的分析工件产物。
    /// 若找到已完成工件则返回其载荷文本，否则返回 null。
    /// </summary>
    /// <param name="artifactRepo">工件仓储。</param>
    /// <param name="taskId">当前任务 ID。</param>
    /// <param name="artifactType">工件类型标识（如 code_analysis_artifact）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工件载荷 JSON 文本；未找到或未完成则返回 null。</returns>
    private static async Task<string?> TryLoadArtifactByTypeAsync(
        ITaskArtifactRepository artifactRepo,
        Guid taskId,
        string artifactType,
        CancellationToken cancellationToken)
    {
        var artifact = await artifactRepo.GetByTypeAndKeyAsync(taskId, artifactType, "analysis");
        if (artifact is null || artifact.Status != "completed")
            return null;
        return artifact.PayloadJson;
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
    /// <summary>
    /// 在同一数据库事务中完成主链路落库，确保完成态与真实结果一致。
    /// V4：已移除旧 Wiki 实体，不再返回 WikiId。
    /// </summary>
    private static async Task<(Guid RepositoryVersionId, Guid WikiVersionId, List<WikiPage> Pages)> PersistWikiProjectionAsync(
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
        bool debugTruncated = false,
        int debugOriginalPageCount = 0)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            structure.Title,
            structure.Description,
            page_count = structure.Pages.Count,
            repository_version_id = repositoryVersionId,
            wiki_version_id = wikiVersionId,
            debug_truncated = debugTruncated ? true : (bool?)null,
            debug_original_page_count = debugTruncated ? debugOriginalPageCount : (int?)null,
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
    /// 按 BFS 树形拓扑序遍历页面：根节点 (parentId=null) 优先，随后逐层展开子节点。
    /// 确保父页面始终在子页面之前生成。
    /// </summary>
    private static List<WikiPageDto> OrderPagesByTreeBfs(List<WikiPageDto> pages)
    {
        var pageMap = pages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var childrenMap = new Dictionary<string, List<WikiPageDto>>(StringComparer.OrdinalIgnoreCase);
        const string rootKey = "__root__";

        foreach (var page in pages)
        {
            var parentKey = string.IsNullOrWhiteSpace(page.ParentId) ? rootKey : page.ParentId;
            if (!childrenMap.ContainsKey(parentKey))
                childrenMap[parentKey] = new List<WikiPageDto>();
            childrenMap[parentKey].Add(page);
        }

        var result = new List<WikiPageDto>();
        var queue = new Queue<string>();
        queue.Enqueue(rootKey); // 从根节点开始

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenMap.TryGetValue(parentId, out var siblings)) continue;

            // 同层页面按 contentDepthLevel 排序
            var ordered = siblings
                .OrderBy(p => p.ContentDepthLevel switch
                {
                    "overview" => 0,
                    "section" => 1,
                    "article" => 2,
                    _ => 1
                })
                .ToList();

            foreach (var page in ordered)
            {
                result.Add(page);
                queue.Enqueue(page.Id);
            }
        }

        // 确保所有页面都被包含（孤岛页面兜底）
        var includedIds = result.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            if (!includedIds.Contains(page.Id))
                result.Add(page);
        }

        return result;
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
    /// <summary>
    /// V4 构建弱页面的重生成提示词，包含原始内容摘要与质量改进指导。
    /// </summary>
    /// <param name="page">需要重新生成的弱页面。</param>
    /// <param name="fileContents">关联文件内容。</param>
    /// <param name="qualityScore">当前质量评分。</param>
    /// <param name="languageDisplayName">输出语言。</param>
    /// <returns>重生成提示词。</returns>
    private static string BuildRegenerationPrompt(
        WikiPageDto page, string fileContents, int qualityScore, string languageDisplayName)
    {
        var weaknessHint = qualityScore switch
        {
            < 30 => "原始内容过短或质量很低，请提供更详细、更有技术深度的内容。",
            < 45 => "原始内容技术深度不足，请增加代码示例、架构分析和实现细节。",
            _ => "原始内容有一定基础但仍需改进，请增强结构化程度和具体代码引用。"
        };

        // 根据 ContentDepthLevel 附加层级深度符合性要求
        var depthCompliance = page.ContentDepthLevel?.ToLowerInvariant() switch
        {
            "article" => "\n特别要求：这是 Article 级别的深度页面，必须包含代码引用、函数签名和实现细节。缺少代码引用会严重扣分。",
            "overview" => "\n特别要求：这是 Overview 级别的概览页面，应聚焦架构图和组件关系，不需要过深的实现细节。",
            "section" => "\n特别要求：这是 Section 级别的中层页面，应平衡广度和深度，包含类图或流程图。",
            _ => ""
        };

        return $$"""
你是资深技术文档专家。请重新生成以下 Wiki 页面，显著提升内容质量。

页面标题：{{page.Title}}
页面描述：{{page.Description}}
当前质量评分：{{qualityScore}}/100
改进方向：{{weaknessHint}}{{depthCompliance}}

原始内容摘要（需要改进）：
{{(page.Content.Length > 500 ? page.Content[..500] + "..." : page.Content)}}

相关源文件内容：
{{fileContents}}

请生成改进后的完整 Markdown 内容，要求：
1. 提供具体代码引用和实现细节
2. 使用清晰的结构化标题层级
3. 包含代码块、表格等技术元素
4. 确保技术深度和覆盖面

以 {{languageDisplayName}} 输出。
""";
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

    /// <summary>
    /// 构建混合搜索索引（BM25），供页面生成阶段检索真实代码片段。
    /// </summary>
    private async Task BuildSearchIndexAsync(string repoPath, CodeIndexResult codeIndexResult, string indexKey, CancellationToken ct)
    {
        _logger.LogInformation("开始构建搜索索引：{Key}", indexKey);

        var snippets = new List<CodeSnippetInput>();
        var codeIndexService = new Heimdall.Core.Services.Repository.CodeIndexService(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Heimdall.Core.Services.Repository.CodeIndexService>());

        foreach (var entry in codeIndexResult.Entries.Where(e => e.FileType is "source" or "config"))
        {
            try
            {
                var fullPath = Path.Combine(repoPath, entry.FilePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) continue;

                var content = File.ReadAllText(fullPath);
                // 大文件完整保留（不再截断到 5000），让 ContextPackingService 按 Token 预算智能截断
                if (content.Length > 200_000) content = content[..200_000];

                var chunks = codeIndexService.ChunkFile(fullPath, entry.Language);
                var language = entry.Language ?? DetectLanguageFromExtension(entry.FilePath);
                foreach (var (start, end, chunkContent) in chunks.Take(100))
                {
                    snippets.Add(new CodeSnippetInput
                    {
                        FilePath = entry.FilePath,
                        ModuleName = entry.ModuleName,
                        Content = chunkContent,
                        Symbols = string.Join(" ", entry.ExportedSymbols.Take(20)),
                        Language = language,
                        StartLine = start,
                        EndLine = end
                    });
                }
            }
            catch { /* skip */ }
        }

        await _hybridSearch.BuildIndexAsync(indexKey, snippets, ct);
        _logger.LogInformation("搜索索引构建完成：{Key}, {Count} 代码段", indexKey, snippets.Count);
    }

    private static string DetectLanguageFromExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".csproj" => "xml",
            ".sln" => "text",
            ".json" => "json",
            ".xml" => "xml",
            ".config" => "xml",
            ".targets" => "xml",
            ".props" => "xml",
            ".xaml" => "xml",
            ".ts" => "typescript",
            ".tsx" => "typescript",
            ".js" => "javascript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".java" => "java",
            ".rs" => "rust",
            ".rb" => "ruby",
            ".md" => "markdown",
            ".yml" or ".yaml" => "yaml",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".sql" => "sql",
            ".html" => "html",
            ".css" => "css",
            _ => "text"
        };
    }

    /// <summary>
    /// 收集仓库根目录及 docs/、.github/ 目录下的 Markdown 文档文件。
    /// 按优先级排序：AGENTS.md > CLAUDE.md > README.md > CONTRIBUTING.md > 其他。
    /// </summary>
    private static List<RepositoryDoc> CollectRepositoryDocuments(string repoPath)
    {
        var result = new List<RepositoryDoc>();
        var scanDirs = new[] { repoPath, Path.Combine(repoPath, "docs"), Path.Combine(repoPath, ".github") };

        var targetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AGENTS.md", "CLAUDE.md", "README.md", "CONTRIBUTING.md",
            "CODE_OF_CONDUCT.md", "CHANGELOG.md", "SECURITY.md", "GOVERNANCE.md"
        };

        foreach (var dir in scanDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var filePath in Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(filePath);

                // 优先匹配已知的高价值文档，否则需在根目录才收集
                if (!targetFiles.Contains(fileName) && dir != repoPath) continue;

                try
                {
                    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    var priority = GetDocumentPriority(fileName);
                    result.Add(new RepositoryDoc
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        Content = content,
                        Priority = priority
                    });
                }
                catch (Exception)
                {
                    // 文件读取失败不阻塞主流程
                }
            }
        }

        return result
            .OrderBy(d => d.Priority)
            .ThenBy(d => d.FileName)
            .ToList();
    }

    private static int GetDocumentPriority(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "agents.md" => 1,
            "claude.md" => 2,
            "readme.md" => 3,
            "contributing.md" => 4,
            _ => 5
        };
    }

    /// <summary>
    /// 判断页面是否为架构/概览类型，适合注入仓库文档内容。
    /// </summary>
    private static bool IsArchitectureOrOverviewPage(WikiPageDto page)
    {
        if (string.Equals(page.ContentDepthLevel, "overview", StringComparison.OrdinalIgnoreCase))
            return true;

        var titleAndDesc = $"{page.Title} {page.Description}".ToLowerInvariant();
        var architectureKeywords = new[] { "架构", "architecture", "模块", "module", "设计", "design",
            "概述", "overview", "总览", "结构", "structure", "分层", "layer", "依赖", "dependency" };

        return architectureKeywords.Any(k => titleAndDesc.Contains(k));
    }

    /// <summary>
    /// 构建注入提示词的仓库文档文本，按优先级排序并在超出预算时裁剪。
    /// </summary>
    private static string BuildRepositoryDocsSection(List<RepositoryDoc> docs, int maxChars = 8000)
    {
        if (docs.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 仓库文档参考");
        sb.AppendLine("以下为仓库根目录文档内容，请据此理解项目架构和组织方式：");
        sb.AppendLine();

        var remaining = maxChars;

        foreach (var doc in docs.OrderBy(d => d.Priority))
        {
            var content = doc.Content;
            if (content.Length > 5000)
            {
                content = content[..3000] + "\n\n…（文档过长，已截断）";
            }

            var entry = $"### {doc.FileName}\n{content}\n\n";
            if (entry.Length > remaining && sb.Length > 0)
            {
                sb.AppendLine("…（后续文档因预算不足已省略）");
                break;
            }

            sb.Append(entry);
            remaining -= entry.Length;
        }

        return sb.ToString();
    }
}

/// <summary>
/// 仓库根目录文档记录。
/// </summary>
public record RepositoryDoc
{
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int Priority { get; init; } = 5;
}
