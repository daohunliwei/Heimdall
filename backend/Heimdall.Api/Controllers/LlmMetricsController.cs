using Heimdall.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api")]
public class LlmMetricsController : ControllerBase
{
    private readonly ILlmObservabilityService _observability;

    public LlmMetricsController(ILlmObservabilityService observability)
    {
        _observability = observability;
    }

    /// <summary>
    /// 获取指定任务的 LLM 调用指标汇总。
    /// </summary>
    [HttpGet("tasks/{taskId:guid}/metrics")]
    public async Task<IActionResult> GetTaskMetrics(Guid taskId, CancellationToken ct)
    {
        var summary = await _observability.GetTaskSummaryAsync(taskId, ct);
        return Ok(summary);
    }

    /// <summary>
    /// 获取指定任务的 LLM 调用明细列表。
    /// </summary>
    [HttpGet("tasks/{taskId:guid}/metrics/details")]
    public async Task<IActionResult> GetTaskMetricsDetails(Guid taskId, CancellationToken ct)
    {
        var metrics = await _observability.GetTaskMetricsAsync(taskId, ct);
        return Ok(metrics);
    }

    /// <summary>
    /// 管理端点：查询时间范围内的 LLM 调用指标。
    /// </summary>
    [HttpGet("admin/llm-metrics")]
    public async Task<IActionResult> GetMetricsByTimeRange(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-7);
        var end = to ?? DateTime.UtcNow;
        var metrics = await _observability.GetMetricsByTimeRangeAsync(start, end, ct);
        return Ok(metrics);
    }
}
