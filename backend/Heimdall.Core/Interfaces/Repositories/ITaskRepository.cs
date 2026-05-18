using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="TaskRecord"/> entities.</summary>
public interface ITaskRepository
{
    Task<TaskRecord?> GetByIdAsync(Guid id);
    Task<TaskRecord?> GetByRepoAndBranchAsync(Guid repositoryId, string sourceBranch);
    Task<TaskRecord?> GetRunningByRepoAndBranchAsync(Guid repositoryId, string sourceBranch);
    Task<TaskRecord?> GetPendingByRepoBranchTypeAsync(Guid repositoryId, string sourceBranch, string taskType);
    Task<TaskRecord?> GetCompletedByHashAsync(string requestHash);
    Task<List<TaskRecord>> GetRecoverableAsync(string taskType);

    /// <summary>Atomically upserts a task: inserts if no matching pending/running task exists, otherwise returns the existing one.</summary>
    Task<TaskRecord> EnqueueAsync(TaskRecord task);

    /// <summary>
    /// 持久化任务实体上的完整字段变更。
    /// 适用于状态之外还包含结果版本、结果摘要或错误上下文等字段更新的场景。
    /// </summary>
    Task<TaskRecord> UpdateAsync(TaskRecord task);

    Task<TaskRecord> UpdateStatusAsync(Guid id, string status, int? progressPercent = null, string? progressMessage = null, string? errorMessage = null);
    Task IncrementTokensAsync(Guid taskId, int promptTokens, int completionTokens);
    Task<(List<TaskRecord> Items, int TotalCount)> GetAllAsync(string? status = null, string? taskType = null, Guid? repositoryId = null, int offset = 0, int limit = 20);
}
