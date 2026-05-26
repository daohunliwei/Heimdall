using System.Threading.Channels;
using System.Collections.Concurrent;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 后端统一任务队列。
/// 当前阶段优先承载 Wiki 任务，负责在 API 返回后异步执行真正的 Wiki 生成流程。
/// </summary>
public sealed class TaskQueueService : BackgroundService, ITaskQueueService
{
    private readonly Channel<TaskEnqueueRequest> _channel = Channel.CreateUnbounded<TaskEnqueueRequest>();
    private readonly ConcurrentDictionary<Guid, byte> _queuedWikiTasks = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WikiTaskService _wikiTaskService;
    private readonly ILogger<TaskQueueService> _logger;

    public TaskQueueService(
        IServiceScopeFactory scopeFactory,
        WikiTaskService wikiTaskService,
        ILogger<TaskQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _wikiTaskService = wikiTaskService;
        _logger = logger;
    }

    /// <summary>
    /// 根据请求创建任务记录并入队。
    /// 若调用方已经通过其他服务创建了任务记录，优先使用 <see cref="QueueWikiTaskAsync"/> 避免重复落库。
    /// </summary>
    public async Task<TaskRecord> EnqueueAsync(TaskEnqueueRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        if (request.RepositoryId.HasValue)
        {
            var running = await taskRepo.GetRunningByRepoAndBranchAsync(request.RepositoryId.Value, request.SourceBranch);
            if (running is not null) return running;
            var pending = await taskRepo.GetPendingByRepoBranchTypeAsync(request.RepositoryId.Value, request.SourceBranch, request.TaskType);
            if (pending is not null) return pending;
        }

        var task = new TaskRecord
        {
            TaskType = request.TaskType,
            Status = "pending",
            RepositoryId = request.RepositoryId,
            SourceBranch = request.SourceBranch,
            UserId = request.UserId,
            RequestHash = request.RequestHash,
            Provider = request.Provider,
            Model = request.Model,
            Language = request.Language,
            ProgressPercent = 0,
            ProgressMessage = "等待执行..."
        };

        var created = await taskRepo.EnqueueAsync(task);
        request.TaskId = created.Id;
        await _channel.Writer.WriteAsync(request);
        _logger.LogInformation("任务入队 TaskId={TaskId} Type={TaskType}", created.Id, created.TaskType);
        return created;
    }

    /// <summary>
    /// 将已落库的 Wiki 任务写入统一队列。
    /// 该方法会基于任务标识做进程内去重，防止同一 pending 任务被多个入口重复调度。
    /// </summary>
    public async Task QueueWikiTaskAsync(TaskRecord task, TaskEnqueueRequest request, CancellationToken cancellationToken = default)
    {
        if (task.Status != "pending")
        {
            _logger.LogInformation("跳过非 pending Wiki 任务入队 TaskId={TaskId} Status={Status}", task.Id, task.Status);
            return;
        }

        if (!_queuedWikiTasks.TryAdd(task.Id, 0))
        {
            _logger.LogInformation("Wiki 任务已在队列中 TaskId={TaskId}", task.Id);
            return;
        }

        request.TaskId = task.Id;
        request.RepositoryId ??= task.RepositoryId;
        request.SourceBranch = string.IsNullOrWhiteSpace(request.SourceBranch) ? task.SourceBranch : request.SourceBranch;
        request.Provider ??= task.Provider;
        request.Model ??= task.Model;
        request.Language ??= task.Language;
        request.ForceRefresh = request.ForceRefresh || task.ForceRefresh;
        request.GenerationProfile = string.IsNullOrWhiteSpace(request.GenerationProfile)
            ? "comprehensive"
            : request.GenerationProfile;

        await _channel.Writer.WriteAsync(request, cancellationToken);
        _logger.LogInformation("Wiki 任务已提交到统一队列 TaskId={TaskId}", task.Id);
    }

    /// <summary>
    /// 按任务标识查询当前状态。
    /// </summary>
    public async Task<TaskRecord?> GetStatusAsync(Guid taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        return await taskRepo.GetByIdAsync(taskId);
    }

    /// <summary>
    /// 将任务标记为已取消。
    /// </summary>
    public async Task CancelAsync(Guid taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await taskRepo.UpdateStatusAsync(taskId, "cancelled", errorMessage: "用户取消");
    }

    /// <summary>
    /// 将已存在的 Wiki 任务重新写回统一队列。
    /// 该方法用于失败重试、手工恢复以及进程重启后的任务补偿。
    /// </summary>
    public async Task RequeueWikiTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var repoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

        var task = await taskRepo.GetByIdAsync(taskId);
        if (task is null)
        {
            _logger.LogWarning("重入队失败：任务不存在 TaskId={TaskId}", taskId);
            return;
        }

        if (!string.Equals(task.TaskType, "wiki", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("重入队失败：暂不支持的任务类型 TaskId={TaskId} Type={TaskType}", taskId, task.TaskType);
            return;
        }

        if (!task.RepositoryId.HasValue)
        {
            _logger.LogWarning("重入队失败：任务缺少仓库标识 TaskId={TaskId}", taskId);
            return;
        }

        var repo = await repoRepo.GetByIdAsync(task.RepositoryId.Value);
        if (repo is null)
        {
            _logger.LogWarning("重入队失败：仓库不存在 TaskId={TaskId} RepoId={RepoId}", taskId, task.RepositoryId.Value);
            return;
        }

        task.Status = "pending";
        task.ProgressPercent = Math.Min(task.ProgressPercent, 90);
        task.ProgressMessage = string.IsNullOrWhiteSpace(task.LastSuccessfulStage)
            ? "任务已重新排队，等待恢复执行..."
            : $"任务已重新排队，将从阶段 {task.LastSuccessfulStage} 后恢复";
        task.CurrentStageStatus = "pending";
        task.ErrorMessage = null;
        task.CompletedAt = null;
        await taskRepo.UpdateAsync(task);

        await QueueWikiTaskAsync(task, BuildWikiEnqueueRequest(task, repo), cancellationToken);
    }

    /// <summary>
    /// 服务启动时恢复数据库中未终结的 Wiki 任务，避免进程重启后出现“挂起但无人执行”的情况。
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        await RecoverWikiTasksAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Worker 开始处理任务 Type={TaskType} TaskId={TaskId}", request.TaskType, request.TaskId);

                if (string.Equals(request.TaskType, "wiki", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteWikiTaskAsync(request, stoppingToken);
                    continue;
                }

                _logger.LogWarning("暂不支持的任务类型 Type={TaskType}", request.TaskType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "任务队列执行失败 TaskId={TaskId} Type={TaskType}", request.TaskId, request.TaskType);
            }
            finally
            {
                if (request.TaskId.HasValue)
                    _queuedWikiTasks.TryRemove(request.TaskId.Value, out _);
            }
        }
    }

    /// <summary>
    /// 执行 Wiki 任务。
    /// Worker 会重新读取任务记录，确保状态与数据库中的最终版本关联字段保持一致。
    /// </summary>
    private async Task ExecuteWikiTaskAsync(TaskEnqueueRequest request, CancellationToken stoppingToken)
    {
        if (!request.TaskId.HasValue)
        {
            _logger.LogWarning("跳过缺少 TaskId 的 Wiki 队列消息");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.RepoUrl) || string.IsNullOrWhiteSpace(request.RepoType))
        {
            _logger.LogWarning("跳过缺少仓库上下文的 Wiki 任务 TaskId={TaskId}", request.TaskId.Value);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var task = await taskRepo.GetByIdAsync(request.TaskId.Value);
        if (task is null)
        {
            _logger.LogWarning("Wiki 任务不存在，无法执行 TaskId={TaskId}", request.TaskId.Value);
            return;
        }

        if (task.Status == "running")
        {
            _logger.LogInformation("Wiki 任务已在执行中，跳过重复消费 TaskId={TaskId}", task.Id);
            return;
        }

        if (task.Status is "completed" or "failed" or "cancelled")
        {
            _logger.LogInformation("Wiki 任务状态已终结，跳过执行 TaskId={TaskId} Status={Status}", task.Id, task.Status);
            return;
        }

        await _wikiTaskService.ExecuteAsync(
            task,
            request.RepoUrl,
            request.RepoType,
            request.Token,
            request.Provider,
            request.Model,
            request.CustomModel,
            request.Language ?? "zh",
            request.Comprehensive,
            stoppingToken,
            request.SourceBranch,
            request.GenerationProfile);
    }

    /// <summary>
    /// 恢复所有待执行的 Wiki 任务。
    /// </summary>
    private async Task RecoverWikiTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var repoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryConfigRepository>();

        var tasks = await taskRepo.GetRecoverableAsync("wiki");
        if (tasks.Count == 0)
        {
            _logger.LogInformation("未发现需要恢复的 Wiki 任务");
            return;
        }

        // 批量加载所有引用的仓库
        var repoIds = tasks.Where(t => t.RepositoryId.HasValue)
            .Select(t => t.RepositoryId!.Value).Distinct().ToList();
        var repoMap = (await repoRepo.GetByIdsAsync(repoIds)).ToDictionary(r => r.Id);

        foreach (var task in tasks)
        {
            if (!task.RepositoryId.HasValue)
            {
                _logger.LogWarning("跳过缺少仓库标识的恢复任务 TaskId={TaskId}", task.Id);
                continue;
            }

            if (!repoMap.TryGetValue(task.RepositoryId.Value, out var repo))
            {
                _logger.LogWarning("跳过仓库不存在的恢复任务 TaskId={TaskId} RepoId={RepoId}", task.Id, task.RepositoryId.Value);
                continue;
            }

            if (task.Status == "running")
            {
                task.Status = "pending";
                task.CurrentStageStatus = "pending";
                task.ProgressMessage = string.IsNullOrWhiteSpace(task.LastSuccessfulStage)
                    ? "服务重启后恢复执行"
                    : $"服务重启后恢复执行，将从阶段 {task.LastSuccessfulStage} 后继续";
                await taskRepo.UpdateAsync(task);
            }

            await QueueWikiTaskAsync(task, BuildWikiEnqueueRequest(task, repo), cancellationToken);
        }
    }

    /// <summary>
    /// 根据已有任务与仓库信息构造可恢复的 Wiki 入队负载。
    /// </summary>
    private static TaskEnqueueRequest BuildWikiEnqueueRequest(TaskRecord task, RepositoryEntity repo)
    {
        var repoUrl = repo.RepoUrl ?? $"https://github.com/{repo.Owner}/{repo.RepoName}";

        return new TaskEnqueueRequest
        {
            TaskId = task.Id,
            RepositoryId = repo.Id,
            TaskType = "wiki",
            SourceBranch = string.IsNullOrWhiteSpace(task.TargetBranch) ? task.SourceBranch : task.TargetBranch!,
            UserId = task.UserId,
            Provider = task.Provider,
            Model = task.Model,
            Language = task.Language,
            RequestHash = task.RequestHash,
            RepoUrl = repoUrl,
            RepoType = repo.RepoType,
            ForceRefresh = task.ForceRefresh,
            Comprehensive = true,
            GenerationProfile = "comprehensive"
        };
    }
}
