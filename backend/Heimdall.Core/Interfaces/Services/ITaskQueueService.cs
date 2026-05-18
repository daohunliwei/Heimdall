using Heimdall.Core.Entities;
using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// 统一任务队列接口，负责入队、状态查询、取消以及显式调度 Wiki 任务执行。
/// </summary>
public interface ITaskQueueService
{
    /// <summary>
    /// 根据入队请求创建任务记录并写入队列。
    /// 该方法适用于需要由队列统一创建任务记录的通用场景。
    /// </summary>
    Task<TaskRecord> EnqueueAsync(TaskEnqueueRequest request);

    /// <summary>
    /// 将已创建好的 Wiki 任务显式写入执行队列。
    /// 该方法用于避免控制器直接后台启动线程，并保证同一任务只会被队列消费一次。
    /// </summary>
    Task QueueWikiTaskAsync(TaskRecord task, TaskEnqueueRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据任务标识读取当前任务状态；若任务不存在则返回 <c>null</c>。
    /// </summary>
    Task<TaskRecord?> GetStatusAsync(Guid taskId);

    /// <summary>
    /// 取消一个处于等待中或执行中的任务。
    /// </summary>
    Task CancelAsync(Guid taskId);

    /// <summary>
    /// 重新调度一个已存在的 Wiki 任务。
    /// 该方法主要用于失败重试或服务重启后的恢复调度。
    /// </summary>
    Task RequeueWikiTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
