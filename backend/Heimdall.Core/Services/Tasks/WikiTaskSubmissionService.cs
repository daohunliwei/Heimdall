using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Wiki 任务统一提交服务。
/// 该服务把 `/tasks/wiki` 与 `/wiki/refresh` 的创建、去重、复用与队列调度逻辑收敛到同一处。
/// </summary>
public sealed class WikiTaskSubmissionService : IWikiTaskSubmissionService
{
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IWikiVersionRepository _wikiVersionRepo;
    private readonly IRefreshOrchestrationService _refreshService;
    private readonly WikiTaskService _wikiTaskService;
    private readonly ITaskQueueService _taskQueueService;
    private readonly ILogger<WikiTaskSubmissionService> _logger;

    /// <summary>
    /// 初始化统一 Wiki 提交服务。
    /// </summary>
    public WikiTaskSubmissionService(
        IRepositoryConfigRepository repoRepo,
        IWikiSpaceRepository spaceRepo,
        IWikiVersionRepository wikiVersionRepo,
        IRefreshOrchestrationService refreshService,
        WikiTaskService wikiTaskService,
        ITaskQueueService taskQueueService,
        ILogger<WikiTaskSubmissionService> logger)
    {
        _repoRepo = repoRepo;
        _spaceRepo = spaceRepo;
        _wikiVersionRepo = wikiVersionRepo;
        _refreshService = refreshService;
        _wikiTaskService = wikiTaskService;
        _taskQueueService = taskQueueService;
        _logger = logger;
    }

    /// <summary>
    /// 直接提交 Wiki 生成任务，不执行额外版本发现。
    /// </summary>
    public async Task<WikiTaskSubmissionResult> SubmitGenerateAsync(
        WikiTaskSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var repo = await GetRequiredRepositoryAsync(request.RepositoryId);
        var branch = ResolveBranch(repo, request.Branch);
        var language = ResolveLanguage(repo, request.Language);
        var generationProfile = ResolveGenerationProfile(request.GenerationProfile);
        var task = await CreateOrReuseTaskAsync(request, repo, branch, language, generationProfile);

        await QueuePendingTaskAsync(task, repo, request, branch, language, generationProfile, cancellationToken);

        return new WikiTaskSubmissionResult
        {
            TaskId = task.Id,
            TaskStatus = task.Status,
            RepositoryVersionId = task.ResolvedRepositoryVersionId,
            WikiVersionId = task.ResultWikiVersionId,
            ResultType = task.Status == "completed" ? "reused" : "queued",
            ChangeStatus = task.Status == "completed" ? "unchanged" : "changed",
            Message = task.Status == "completed"
                ? "复用已有 Wiki 结果"
                : task.Status == "running"
                    ? "已有相同任务正在执行"
                    : "任务已接收，进入统一队列执行"
        };
    }

    /// <summary>
    /// 提交刷新任务：先做版本决策，再决定是否复用现有结果或排队新的 Wiki 任务。
    /// </summary>
    public async Task<WikiTaskSubmissionResult> SubmitRefreshAsync(
        WikiTaskSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var repo = await GetRequiredRepositoryAsync(request.RepositoryId);
        var branch = ResolveBranch(repo, request.Branch);
        var language = ResolveLanguage(repo, request.Language);
        var generationProfile = ResolveGenerationProfile(request.GenerationProfile);

        var refreshResult = await _refreshService.RefreshAsync(new RefreshRequest
        {
            RepositoryId = request.RepositoryId,
            Branch = branch,
            RefreshStrategy = string.IsNullOrWhiteSpace(request.RefreshStrategy) ? "latest" : request.RefreshStrategy!,
            ForceRefresh = request.ForceRefresh,
            Provider = request.Provider,
            Model = request.Model ?? request.CustomModel,
            Language = language,
            GenerationProfile = generationProfile
        }, cancellationToken);

        if (refreshResult.ResultType == "queued")
        {
            var task = await CreateOrReuseTaskAsync(request, repo, branch, language, generationProfile);
            await QueuePendingTaskAsync(task, repo, request, branch, language, generationProfile, cancellationToken);

            return new WikiTaskSubmissionResult
            {
                TaskId = task.Id,
                TaskStatus = task.Status,
                RepositoryVersionId = refreshResult.RepositoryVersionId ?? task.ResolvedRepositoryVersionId,
                WikiVersionId = refreshResult.WikiVersionId ?? task.ResultWikiVersionId,
                ResultType = "queued",
                ChangeStatus = refreshResult.ChangeStatus,
                Message = task.Status == "running"
                    ? "已有相同刷新任务正在执行"
                    : "刷新任务已进入统一队列"
            };
        }

        var effectiveWikiVersionId = refreshResult.WikiVersionId
            ?? await ResolveEffectiveWikiVersionIdAsync(request.RepositoryId, language);

        return new WikiTaskSubmissionResult
        {
            TaskId = refreshResult.TaskId,
            TaskStatus = refreshResult.ResultType == "reused" ? "completed" : "completed",
            RepositoryVersionId = refreshResult.RepositoryVersionId,
            WikiVersionId = effectiveWikiVersionId,
            ResultType = refreshResult.ResultType,
            ChangeStatus = refreshResult.ChangeStatus,
            Message = refreshResult.Message
        };
    }

    /// <summary>
    /// 读取并校验仓库配置。
    /// 若仓库不存在则抛出异常，由上层控制器统一转换为响应。
    /// </summary>
    private async Task<RepositoryEntity> GetRequiredRepositoryAsync(Guid repositoryId)
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null)
            throw new InvalidOperationException($"仓库不存在：{repositoryId}");

        return repo;
    }

    /// <summary>
    /// 创建任务，或复用已存在的 running / pending / completed 任务。
    /// </summary>
    private async Task<TaskRecord> CreateOrReuseTaskAsync(
        WikiTaskSubmissionRequest request,
        RepositoryEntity repo,
        string branch,
        string language,
        string generationProfile)
    {
        var repoUrl = repo.RepoUrl ?? $"https://github.com/{repo.Owner}/{repo.RepoName}";
        return await _wikiTaskService.CreateTaskAsync(
            repoUrl,
            repo.RepoType,
            request.Token,
            request.Provider,
            request.Model,
            request.CustomModel,
            language,
            request.Comprehensive,
            request.ForceRefresh,
            request.UserId,
            branch,
            string.IsNullOrWhiteSpace(request.RefreshStrategy) ? "latest" : request.RefreshStrategy!,
            generationProfile);
    }

    /// <summary>
    /// 当任务处于 pending 时，将其写入统一 Worker 队列。
    /// 其他状态说明任务已在执行或已可复用，无需再次调度。
    /// </summary>
    private async Task QueuePendingTaskAsync(
        TaskRecord task,
        RepositoryEntity repo,
        WikiTaskSubmissionRequest request,
        string branch,
        string language,
        string generationProfile,
        CancellationToken cancellationToken)
    {
        if (task.Status != "pending")
            return;

        var repoUrl = repo.RepoUrl ?? $"https://github.com/{repo.Owner}/{repo.RepoName}";
        await _taskQueueService.QueueWikiTaskAsync(task, new TaskEnqueueRequest
        {
            TaskId = task.Id,
            RepositoryId = repo.Id,
            TaskType = "wiki",
            SourceBranch = branch,
            UserId = request.UserId,
            Provider = request.Provider,
            Model = request.Model,
            Language = language,
            RequestHash = task.RequestHash,
            RepoUrl = repoUrl,
            RepoType = repo.RepoType,
            Token = request.Token,
            CustomModel = request.CustomModel,
            ForceRefresh = request.ForceRefresh,
            Comprehensive = request.Comprehensive,
            GenerationProfile = generationProfile
        }, cancellationToken);

        _logger.LogInformation("Wiki 任务统一提交完成 TaskId={TaskId} RepoId={RepoId}", task.Id, repo.Id);
    }

    /// <summary>
    /// 解析当前仓库在指定语言下可读的有效 WikiVersion。
    /// 优先读取发布版本，若尚未发布则回退到最新版本。
    /// </summary>
    private async Task<Guid?> ResolveEffectiveWikiVersionIdAsync(Guid repositoryId, string language)
    {
        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, "default");
        if (space is null)
            return null;

        if (space.PublishedWikiVersionId.HasValue)
            return space.PublishedWikiVersionId.Value;

        var versions = await _wikiVersionRepo.GetBySpaceIdAsync(space.Id);
        return versions.OrderByDescending(v => v.VersionNo).FirstOrDefault()?.Id;
    }

    /// <summary>
    /// 解析分支名；若请求未指定则使用仓库默认分支。
    /// </summary>
    private static string ResolveBranch(RepositoryEntity repo, string? requestedBranch)
    {
        return string.IsNullOrWhiteSpace(requestedBranch)
            ? (repo.DefaultBranch ?? "main")
            : requestedBranch;
    }

    /// <summary>
    /// 解析语言；若请求未指定则使用仓库默认语言，最终兜底为中文。
    /// </summary>
    private static string ResolveLanguage(RepositoryEntity repo, string? requestedLanguage)
    {
        return string.IsNullOrWhiteSpace(requestedLanguage)
            ? (repo.DefaultLanguage ?? "zh")
            : requestedLanguage;
    }

    /// <summary>
    /// 解析生成档位；若请求未指定则使用 comprehensive。
    /// </summary>
    private static string ResolveGenerationProfile(string? generationProfile)
    {
        return string.IsNullOrWhiteSpace(generationProfile) ? "comprehensive" : generationProfile;
    }
}
