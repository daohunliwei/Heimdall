using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class TaskLlmCallLogRepository : BaseRepository<TaskLlmCallLog>, ITaskLlmCallLogRepository
{
    public TaskLlmCallLogRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<TaskLlmCallLog> AddAsync(TaskLlmCallLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        await Context.Insertable(log).ExecuteCommandAsync();
        return log;
    }

    public async Task<List<TaskLlmCallLog>> GetByTaskIdAsync(Guid taskId)
    {
        return await Context.Queryable<TaskLlmCallLog>()
            .Where(l => l.TaskId == taskId)
            .OrderBy(l => l.StepOrder)
            .ToListAsync();
    }

    public async Task<(int PromptTokens, int CompletionTokens)> GetTokenSummaryAsync(Guid taskId)
    {
        var logs = await Context.Queryable<TaskLlmCallLog>()
            .Where(l => l.TaskId == taskId)
            .ToListAsync();

        return (
            PromptTokens: logs.Sum(l => l.PromptTokens),
            CompletionTokens: logs.Sum(l => l.CompletionTokens)
        );
    }
}
