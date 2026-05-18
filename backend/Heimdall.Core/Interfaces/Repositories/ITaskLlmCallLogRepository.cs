using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="TaskLlmCallLog"/> entities (LLM call audit trail).</summary>
public interface ITaskLlmCallLogRepository
{
    Task<TaskLlmCallLog> AddAsync(TaskLlmCallLog log);
    Task<List<TaskLlmCallLog>> GetByTaskIdAsync(Guid taskId);

    /// <summary>Returns (totalPromptTokens, totalCompletionTokens) aggregated across the given task's logs.</summary>
    Task<(int PromptTokens, int CompletionTokens)> GetTokenSummaryAsync(Guid taskId);
}
