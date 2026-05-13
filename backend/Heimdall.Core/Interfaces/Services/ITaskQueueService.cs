using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>Task queue management: enqueue, query status, and cancel tasks.</summary>
public interface ITaskQueueService
{
    /// <summary>Enqueue a new task and return its record.</summary>
    Task<TaskRecord> EnqueueAsync(TaskEnqueueRequest request);
    /// <summary>Get the current status of a task by ID.</summary>
    Task<TaskRecord> GetStatusAsync(Guid taskId);
    /// <summary>Cancel a pending or running task.</summary>
    Task CancelAsync(Guid taskId);
}

/// <summary>Request to enqueue a new task.</summary>
public class TaskEnqueueRequest
{
    public string TaskType { get; init; } = "wiki";
    public Guid? RepositoryId { get; init; }
    public string? SourceBranch { get; init; }
    public Guid? UserId { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? Language { get; init; }
    public string? RequestPayload { get; init; }
}
