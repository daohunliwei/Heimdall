using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class LlmMetricsRepository : BaseRepository<LlmCallMetric>, ILlmMetricsRepository
{
    public LlmMetricsRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task AddAsync(LlmCallMetric metric, CancellationToken ct = default)
    {
        await Context.Insertable(metric).ExecuteCommandAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<LlmCallMetric> metrics, CancellationToken ct = default)
    {
        await Context.Insertable(metrics.ToList()).ExecuteCommandAsync(ct);
    }

    public async Task<List<LlmCallMetric>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await Context.Queryable<LlmCallMetric>()
            .Where(m => m.TaskId == taskId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<LlmCallMetric>> GetByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await Context.Queryable<LlmCallMetric>()
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, LlmTaskMetricsSummary>> GetSummariesByTaskIdsAsync(IEnumerable<Guid> taskIds, CancellationToken ct = default)
    {
        var ids = taskIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, LlmTaskMetricsSummary>();

        var raw = await Context.Queryable<LlmCallMetric>()
            .Where(m => ids.Contains(m.TaskId))
            .Select(m => new
            {
                m.TaskId,
                m.InputTokens,
                m.OutputTokens,
                m.CacheHitTokens,
                m.LatencyMs,
                m.Success,
                m.Stage
            })
            .ToListAsync(ct);

        return raw.GroupBy(r => r.TaskId).ToDictionary(g => g.Key, g =>
        {
            var list = g.ToList();
            return new LlmTaskMetricsSummary
            {
                TaskId = g.Key,
                TotalCalls = list.Count,
                TotalInputTokens = list.Sum(m => (long)m.InputTokens),
                TotalOutputTokens = list.Sum(m => (long)m.OutputTokens),
                TotalCacheHitTokens = list.Sum(m => (long)m.CacheHitTokens),
                CacheHitRate = list.Sum(m => m.InputTokens) > 0
                    ? (double)list.Sum(m => m.CacheHitTokens) / list.Sum(m => m.InputTokens) : 0,
                AverageLatencyMs = list.Average(m => m.LatencyMs),
                MaxLatencyMs = list.Max(m => m.LatencyMs),
                FailedCalls = list.Count(m => !m.Success)
            };
        });
    }

    public async Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default)
    {
        var stats = await Context.Queryable<LlmCallMetric>()
            .Where(m => m.TaskId == taskId)
            .Select(m => new
            {
                TotalCalls = SqlFunc.AggregateCount(m.TaskId),
                TotalInput = SqlFunc.AggregateSum((long)m.InputTokens),
                TotalOutput = SqlFunc.AggregateSum((long)m.OutputTokens),
                TotalCache = SqlFunc.AggregateSum((long)m.CacheHitTokens),
                AvgLatency = SqlFunc.AggregateAvg(m.LatencyMs),
                MaxLatency = SqlFunc.AggregateMax(m.LatencyMs),
                FailedCount = SqlFunc.AggregateSum(m.Success ? 0 : 1)
            })
            .FirstAsync(ct);

        if (stats.TotalCalls == 0)
            return new LlmTaskMetricsSummary { TaskId = taskId };

        var stages = await Context.Queryable<LlmCallMetric>()
            .Where(m => m.TaskId == taskId)
            .GroupBy(m => m.Stage)
            .Select(m => new LlmStageMetrics
            {
                Stage = m.Stage,
                Calls = SqlFunc.AggregateCount(m.TaskId),
                InputTokens = SqlFunc.AggregateSum((long)m.InputTokens),
                OutputTokens = SqlFunc.AggregateSum((long)m.OutputTokens),
                AverageLatencyMs = SqlFunc.AggregateAvg(m.LatencyMs)
            })
            .ToListAsync(ct);

        return new LlmTaskMetricsSummary
        {
            TaskId = taskId,
            TotalCalls = stats.TotalCalls,
            TotalInputTokens = stats.TotalInput,
            TotalOutputTokens = stats.TotalOutput,
            TotalCacheHitTokens = stats.TotalCache,
            CacheHitRate = stats.TotalInput > 0 ? (double)stats.TotalCache / stats.TotalInput : 0,
            AverageLatencyMs = stats.AvgLatency,
            MaxLatencyMs = (int)stats.MaxLatency,
            FailedCalls = stats.FailedCount,
            Stages = stages
        };
    }
}
