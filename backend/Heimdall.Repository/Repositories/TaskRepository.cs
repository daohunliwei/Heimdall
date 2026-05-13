using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TaskRecord?> GetByIdAsync(Guid id)
    {
        return await _context.Tasks
            .Include(t => t.LlmCallLogs)
            .Include(t => t.WikiPages)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TaskRecord?> GetByRepoAndBranchAsync(Guid repositoryId, string sourceBranch)
    {
        return await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.RepositoryId == repositoryId && t.SourceBranch == sourceBranch);
    }

    public async Task<TaskRecord?> GetRunningByRepoAndBranchAsync(Guid repositoryId, string sourceBranch)
    {
        return await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.RepositoryId == repositoryId
                && t.SourceBranch == sourceBranch
                && t.Status == "running");
    }

    public async Task<TaskRecord?> GetPendingByRepoBranchTypeAsync(Guid repositoryId, string sourceBranch, string taskType)
    {
        return await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.RepositoryId == repositoryId
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
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
            return task;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException pgEx
                && pgEx.SqlState == "23505") // unique_violation
        {
            // Another request for the same task already exists; return the existing one
            var existing = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.RepositoryId == task.RepositoryId
                    && t.SourceBranch == task.SourceBranch
                    && t.TaskType == task.TaskType
                    && (t.Status == "pending" || t.Status == "running"));
            return existing!;
        }
    }

    public async Task<TaskRecord> UpdateStatusAsync(Guid id, string status,
        int? progressPercent = null, string? progressMessage = null, string? errorMessage = null)
    {
        var task = await _context.Tasks.FindAsync(id)
            ?? throw new InvalidOperationException($"Task not found: {id}");

        task.Status = status;
        if (progressPercent.HasValue) task.ProgressPercent = progressPercent.Value;
        if (progressMessage is not null) task.ProgressMessage = progressMessage;
        if (errorMessage is not null) task.ErrorMessage = errorMessage;

        if (status == "running" && task.StartedAt is null) task.StartedAt = DateTime.UtcNow;
        if (status is "completed" or "failed") task.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<(List<TaskRecord> Items, int TotalCount)> GetAllAsync(
        string? status = null, string? taskType = null, Guid? repositoryId = null,
        int offset = 0, int limit = 20)
    {
        var query = _context.Tasks.AsNoTracking();

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
