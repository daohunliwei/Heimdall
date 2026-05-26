namespace Heimdall.Core.Interfaces.Services;

/// <summary>Server-sent events progress streaming for task execution.</summary>
public interface ITaskProgressService
{
    /// <summary>Subscribe to progress events for a task, writing SSE to the output stream.</summary>
    Task SubscribeAsync(Guid taskId, Stream output, CancellationToken ct);
    /// <summary>Publish a progress update for a task.</summary>
    Task PublishProgressAsync(Guid taskId, string phase, int percent, string message);
    /// <summary>Publish task completion with a result payload.</summary>
    Task PublishCompleteAsync(Guid taskId, object result);
    /// <summary>Publish a task error.</summary>
    Task PublishErrorAsync(Guid taskId, string error);
}
