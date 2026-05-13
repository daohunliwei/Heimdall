using System.Threading.Channels;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskQueueService : BackgroundService
{
    private readonly Channel<TaskEnqueueRequest> _channel = Channel.CreateUnbounded<TaskEnqueueRequest>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskQueueService> _logger;

    public TaskQueueService(IServiceScopeFactory scopeFactory, ILogger<TaskQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

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
        await _channel.Writer.WriteAsync(request);
        _logger.LogInformation("任务入队 TaskId={TaskId} Type={TaskType}", created.Id, created.TaskType);
        return created;
    }

    public async Task<TaskRecord?> GetStatusAsync(Guid taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        return await taskRepo.GetByIdAsync(taskId);
    }

    public async Task CancelAsync(Guid taskId)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await taskRepo.UpdateStatusAsync(taskId, "cancelled", errorMessage: "用户取消");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Worker 开始处理任务 Type={TaskType}", request.TaskType);
        }
    }
}
