using Heimdall.Api.Mappings;
using Heimdall.Api.Models;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("admin/tasks")]
[Authorize(Policy = "AdminOnly")]
public class TasksAdminController : ControllerBase
{
    private readonly ITaskRepository _taskRepo;
    private readonly TaskQueueService _taskQueue;
    private readonly ILlmObservabilityService _observability;
    private readonly ILogger<TasksAdminController> _logger;

    public TasksAdminController(ITaskRepository taskRepo, TaskQueueService taskQueue, ILlmObservabilityService observability, ILogger<TasksAdminController> logger)
    {
        _taskRepo = taskRepo;
        _taskQueue = taskQueue;
        _observability = observability;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? taskType = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var (items, total) = await _taskRepo.GetAllAsync(status, taskType, null, offset, limit);

        // 批量获取所有任务的指标（一次查询替代 N 次单独查询）
        var taskIds = items.Select(t => t.Id).ToList();
        Dictionary<Guid, LlmTaskMetricsSummary> metricDict;
        try
        {
            metricDict = await _observability.GetSummariesByTaskIdsAsync(taskIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量获取任务 LLM 指标失败 TaskIds={Count}", taskIds.Count);
            metricDict = new Dictionary<Guid, LlmTaskMetricsSummary>();
        }

        var tasks = items.Select(t =>
        {
            metricDict.TryGetValue(t.Id, out var metrics);
            var basic = t.ToTaskStatusResponse();
            return (object)new
            {
                basic.Id,
                task_type = basic.TaskType,
                basic.Status,
                progress_percent = basic.ProgressPercent,
                progress_message = basic.ProgressMessage,
                error_message = basic.ErrorMessage,
                created_at = basic.CreatedAt,
                started_at = (DateTime?)basic.StartedAt,
                completed_at = (DateTime?)basic.CompletedAt,
                input_tokens = metrics?.TotalInputTokens ?? 0L,
                output_tokens = metrics?.TotalOutputTokens ?? 0L,
                cache_hit_tokens = metrics?.TotalCacheHitTokens ?? 0L,
                estimated_cost = metrics?.EstimatedCost ?? 0m,
                total_calls = metrics?.TotalCalls ?? 0
            };
        }).ToList();
        return Ok(new { tasks, total = total });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _taskQueue.CancelAsync(id);
        return Ok(new { status = "cancelled" });
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task is null) return NotFound();
        await _taskQueue.RequeueWikiTaskAsync(id, HttpContext.RequestAborted);
        return Ok(new { status = "pending", message = "任务已重新排队，将按已落库工件恢复执行" });
    }

    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(Guid id, CancellationToken ct)
    {
        var metrics = await _observability.GetTaskMetricsAsync(id, ct);
        var details = metrics.Select(m => new
        {
            stage = m.Stage,
            provider = m.Provider,
            model = m.Model,
            inputTokens = m.InputTokens,
            outputTokens = m.OutputTokens,
            cacheHitTokens = m.CacheHitTokens,
            latencyMs = m.LatencyMs,
            success = m.Success,
            errorType = m.ErrorType,
            createdAt = m.CreatedAt
        });
        return Ok(details);
    }
}
