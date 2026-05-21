using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>
/// LLM 调用指标仓储接口。
/// </summary>
public interface ILlmMetricsRepository
{
    /// <summary>记录一次 LLM 调用指标。</summary>
    Task AddAsync(LlmCallMetric metric, CancellationToken ct = default);

    /// <summary>批量记录 LLM 调用指标。</summary>
    Task AddRangeAsync(IEnumerable<LlmCallMetric> metrics, CancellationToken ct = default);

    /// <summary>按 TaskId 查询所有调用指标。</summary>
    Task<List<LlmCallMetric>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>按时间范围查询调用指标。</summary>
    Task<List<LlmCallMetric>> GetByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>按 TaskId 聚合 Token 消耗统计。</summary>
    Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default);
}

/// <summary>
/// 按任务汇总的 LLM 调用指标。
/// </summary>
public class LlmTaskMetricsSummary
{
    public Guid TaskId { get; set; }
    public int TotalCalls { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalCacheHitTokens { get; set; }
    public double CacheHitRate { get; set; }
    public double AverageLatencyMs { get; set; }
    public int MaxLatencyMs { get; set; }
    public int FailedCalls { get; set; }
    public decimal EstimatedCost { get; set; }
    public List<LlmStageMetrics> Stages { get; set; } = new();
}

/// <summary>
/// 按阶段汇总的指标。
/// </summary>
public class LlmStageMetrics
{
    public string Stage { get; set; } = string.Empty;
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public double AverageLatencyMs { get; set; }
}
