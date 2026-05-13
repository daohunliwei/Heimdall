using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class TaskLlmCallLogRepository : ITaskLlmCallLogRepository
{
    private readonly AppDbContext _context;

    public TaskLlmCallLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TaskLlmCallLog> AddAsync(TaskLlmCallLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        _context.TaskLlmCallLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<List<TaskLlmCallLog>> GetByTaskIdAsync(Guid taskId)
    {
        return await _context.TaskLlmCallLogs
            .AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .OrderBy(l => l.StepOrder)
            .ToListAsync();
    }

    public async Task<(int PromptTokens, int CompletionTokens)> GetTokenSummaryAsync(Guid taskId)
    {
        var logs = await _context.TaskLlmCallLogs
            .AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .ToListAsync();

        return (
            PromptTokens: logs.Sum(l => l.PromptTokens),
            CompletionTokens: logs.Sum(l => l.CompletionTokens)
        );
    }
}
