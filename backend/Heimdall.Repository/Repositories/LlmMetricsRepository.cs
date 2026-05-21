using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class LlmMetricsRepository : ILlmMetricsRepository
{
    private readonly AppDbContext _context;

    public LlmMetricsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LlmCallMetric metric, CancellationToken ct = default)
    {
        _context.LlmCallMetrics.Add(metric);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<LlmCallMetric> metrics, CancellationToken ct = default)
    {
        _context.LlmCallMetrics.AddRange(metrics);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<LlmCallMetric>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _context.LlmCallMetrics
            .AsNoTracking()
            .Where(m => m.TaskId == taskId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<LlmCallMetric>> GetByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _context.LlmCallMetrics
            .AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default)
    {
        var metrics = await _context.LlmCallMetrics
            .AsNoTracking()
            .Where(m => m.TaskId == taskId)
            .ToListAsync(ct);

        if (metrics.Count == 0)
        {
            return new LlmTaskMetricsSummary { TaskId = taskId };
        }

        var stages = metrics
            .GroupBy(m => m.Stage)
            .Select(g => new LlmStageMetrics
            {
                Stage = g.Key,
                Calls = g.Count(),
                InputTokens = g.Sum(m => (long)m.InputTokens),
                OutputTokens = g.Sum(m => (long)m.OutputTokens),
                AverageLatencyMs = g.Average(m => m.LatencyMs)
            })
            .ToList();

        return new LlmTaskMetricsSummary
        {
            TaskId = taskId,
            TotalCalls = metrics.Count,
            TotalInputTokens = metrics.Sum(m => (long)m.InputTokens),
            TotalOutputTokens = metrics.Sum(m => (long)m.OutputTokens),
            TotalCacheHitTokens = metrics.Sum(m => (long)m.CacheHitTokens),
            CacheHitRate = metrics.Sum(m => m.InputTokens) > 0
                ? (double)metrics.Sum(m => m.CacheHitTokens) / metrics.Sum(m => m.InputTokens)
                : 0,
            AverageLatencyMs = metrics.Average(m => m.LatencyMs),
            MaxLatencyMs = metrics.Max(m => m.LatencyMs),
            FailedCalls = metrics.Count(m => !m.Success),
            Stages = stages
        };
    }
}
