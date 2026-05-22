using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly ISqlSugarClient _db;

    public TaskRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<TaskRecord?> GetByIdAsync(Guid id)
    {
        return await _db.Queryable<TaskRecord>()
            .FirstAsync(t => t.Id == id);
    }

    public async Task<TaskRecord?> GetByRepoAndBranchAsync(Guid repositoryId, string sourceBranch)
    {
        return await _db.Queryable<TaskRecord>()
            .FirstAsync(t => t.RepositoryId == repositoryId && t.SourceBranch == sourceBranch);
    }

    public async Task<TaskRecord?> GetRunningByRepoAndBranchAsync(Guid repositoryId, string sourceBranch)
    {
        return await _db.Queryable<TaskRecord>()
            .FirstAsync(t => t.RepositoryId == repositoryId
                && t.SourceBranch == sourceBranch
                && t.Status == "running");
    }

    public async Task<TaskRecord?> GetPendingByRepoBranchTypeAsync(Guid repositoryId, string sourceBranch, string taskType)
    {
        return await _db.Queryable<TaskRecord>()
            .FirstAsync(t => t.RepositoryId == repositoryId
                && t.SourceBranch == sourceBranch
                && t.TaskType == taskType
                && t.Status == "pending");
    }

    public async Task<TaskRecord> EnqueueAsync(TaskRecord task)
    {
        try
        {
            task.CreatedAt = DateTime.UtcNow;
            task.Status = "pending";
            await _db.Insertable(task).ExecuteCommandAsync();
            return task;
        }
        catch (Exception)
        {
            // Another request for the same task already exists; return the existing one
            var existing = await _db.Queryable<TaskRecord>()
                .FirstAsync(t => t.RepositoryId == task.RepositoryId
                    && t.SourceBranch == task.SourceBranch
                    && t.TaskType == task.TaskType
                    && (t.Status == "pending" || t.Status == "running"));
            return existing!;
        }
    }

    /// <summary>
    /// 持久化任务实体的完整变更。
    /// 该方法用于保存结果版本、结果摘要、开始/结束时间等非状态字段，避免被 UpdateStatusAsync 丢失。
    /// </summary>
    public async Task<TaskRecord> UpdateAsync(TaskRecord task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        await _db.Updateable(task).ExecuteCommandAsync();
        return task;
    }

    public async Task<TaskRecord> UpdateStatusAsync(Guid id, string status,
        int? progressPercent = null, string? progressMessage = null, string? errorMessage = null)
    {
        for (var retry = 0; retry < 3; retry++)
        {
            try
            {
                var task = await _db.Queryable<TaskRecord>().FirstAsync(t => t.Id == id)
                    ?? throw new InvalidOperationException($"Task not found: {id}");

                task.Status = status;
                if (progressPercent.HasValue) task.ProgressPercent = progressPercent.Value;
                if (progressMessage is not null) task.ProgressMessage = progressMessage;
                if (errorMessage is not null) task.ErrorMessage = errorMessage;

                if (status == "running" && task.StartedAt is null) task.StartedAt = DateTime.UtcNow;
                if (status is "completed" or "failed") task.CompletedAt = DateTime.UtcNow;

                await _db.Updateable(task).ExecuteCommandAsync();
                return task;
            }
            catch (Exception)
            {
                if (retry == 2) throw;
                // 重新加载实体后重试
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    public async Task<TaskRecord?> GetCompletedByHashAsync(string requestHash)
    {
        return await _db.Queryable<TaskRecord>()
            .Where(t => t.RequestHash == requestHash && t.Status == "completed")
            .OrderByDescending(t => t.CompletedAt)
            .FirstAsync();
    }

    /// <summary>
    /// 读取需要恢复调度的任务集合。
    /// 该方法用于服务重启后的恢复，覆盖 pending 与 running 两类未终结任务。
    /// </summary>
    public async Task<List<TaskRecord>> GetRecoverableAsync(string taskType)
    {
        return await _db.Queryable<TaskRecord>()
            .Where(t => t.TaskType == taskType && (t.Status == "pending" || t.Status == "running"))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task IncrementTokensAsync(Guid taskId, int promptTokens, int completionTokens)
    {
        await _db.Updateable<TaskRecord>()
            .SetColumns(t => t.TotalPromptTokens == t.TotalPromptTokens + promptTokens)
            .SetColumns(t => t.TotalCompletionTokens == t.TotalCompletionTokens + completionTokens)
            .Where(t => t.Id == taskId)
            .ExecuteCommandAsync();
    }

    public async Task<(List<TaskRecord> Items, int TotalCount)> GetAllAsync(
        string? status = null, string? taskType = null, Guid? repositoryId = null,
        int offset = 0, int limit = 20)
    {
        var query = _db.Queryable<TaskRecord>();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(taskType))
            query = query.Where(t => t.TaskType == taskType);
        if (repositoryId.HasValue)
            query = query.Where(t => t.RepositoryId == repositoryId.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return (items, totalCount);
    }
}
