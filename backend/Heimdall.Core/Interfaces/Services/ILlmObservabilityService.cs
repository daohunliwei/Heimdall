using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Infrastructure.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// LLM 可观测性服务接口——记录和查询 LLM 调用指标。
/// </summary>
public interface ILlmObservabilityService
{
    /// <summary>
    /// 记录一次 LLM 调用指标。
    /// </summary>
    Task RecordCallAsync(Guid taskId, string stage, string provider, string model,
        ChatCompletionResponse response, CancellationToken ct = default);

    /// <summary>
    /// 获取指定任务的指标汇总。
    /// </summary>
    Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// 获取指定任务的所有调用记录。
    /// </summary>
    Task<List<LlmCallMetric>> GetTaskMetricsAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// 获取时间范围内的调用指标。
    /// </summary>
    Task<List<LlmCallMetric>> GetMetricsByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 估算调用成本。
    /// </summary>
    decimal EstimateCost(string provider, string model, int inputTokens, int outputTokens);
}
