using System.Threading.Channels;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskQueueService : BackgroundService
{
    private readonly Channel<TaskEnqueueRequest> _channel = Channel.CreateUnbounded<TaskEnqueueRequest>();
    private readonly ITaskRepository _taskRepo;
    private readonly TaskProgressService _progressService;
    private readonly TaskLlmCallLogService _llmLogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskQueueService> _logger;

    public TaskQueueService(
        ITaskRepository taskRepo,
        TaskProgressService progressService,
        TaskLlmCallLogService llmLogService,
        IServiceProvider serviceProvider,
        ILogger<TaskQueueService> logger)
    {
        _taskRepo = taskRepo;
        _progressService = progressService;
        _llmLogService = llmLogService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TaskRecord> EnqueueAsync(TaskEnqueueRequest request)
    {
        // 先检查是否有 running 任务
        if (request.RepositoryId.HasValue)
        {
            var running = await _taskRepo.GetRunningByRepoAndBranchAsync(
                request.RepositoryId.Value, request.SourceBranch);
            if (running is not null)
                return running;

            var pending = await _taskRepo.GetPendingByRepoBranchTypeAsync(
                request.RepositoryId.Value, request.SourceBranch, request.TaskType);
            if (pending is not null)
                return pending;
        }

        // 创建新任务
        var task = new TaskRecord
        {
            TaskType = request.TaskType,
            Status = "pending",
            RepositoryId = request.RepositoryId,
            SourceBranch = request.SourceBranch,
            UserId = request.UserId,
            RequestHash = request.RequestHash,
            Provider = request.Provider,
            Model = request.Model ?? request.CustomModel,
            Language = request.Language,
            ProgressPercent = 0,
            ProgressMessage = "等待执行..."
        };

        var created = await _taskRepo.EnqueueAsync(task);
        await _channel.Writer.WriteAsync(request);
        _logger.LogInformation("任务入队 TaskId={TaskId} Type={TaskType}", created.Id, created.TaskType);

        return created;
    }

    public async Task<TaskRecord?> GetStatusAsync(Guid taskId)
    {
        return await _taskRepo.GetByIdAsync(taskId);
    }

    public async Task CancelAsync(Guid taskId)
    {
        await _taskRepo.UpdateStatusAsync(taskId, "cancelled", errorMessage: "用户取消");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            // 找到对应的 task（按 request_hash）
            // 由于每次入队都创建新 task，这里需要获取最近创建的
            _logger.LogInformation("Worker 开始处理任务 Type={TaskType}", request.TaskType);

            try
            {
                // 执行前验证没有运行中的同repo+branch任务
                await ProcessTaskAsync(request, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "任务执行失败");
            }
        }
    }

    private async Task ProcessTaskAsync(TaskEnqueueRequest request, CancellationToken ct)
    {
        // 子类或委托会根据 task_type 调度到具体的 TaskService
        // 这里提供基础进度推送框架
        await _progressService.PublishProgressAsync(Guid.Empty, "prepare", 10, "正在准备...");

        // 具体的任务执行由 WikiTaskService / AskTaskService 等完成
        // 本服务负责队列管理和进度协调
    }
}
