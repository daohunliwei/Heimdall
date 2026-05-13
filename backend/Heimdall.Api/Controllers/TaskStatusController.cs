using Heimdall.Api.Mappings;
using Heimdall.Core.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("tasks")]
public class TaskStatusController : ControllerBase
{
    private readonly TaskQueueService _taskQueue;
    private readonly TaskProgressService _progressService;
    private readonly TaskLlmCallLogService _llmLogService;

    public TaskStatusController(
        TaskQueueService taskQueue,
        TaskProgressService progressService,
        TaskLlmCallLogService llmLogService)
    {
        _taskQueue = taskQueue;
        _progressService = progressService;
        _llmLogService = llmLogService;
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var task = await _taskQueue.GetStatusAsync(id);
        if (task is null) return NotFound(new { error = "任务不存在。" });
        return Ok(task.ToTaskStatusResponse());
    }

    [HttpGet("{id}/stream")]
    public async Task StreamProgress(Guid id, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        await _progressService.SubscribeAsync(id, Response.Body, ct);
    }

    [HttpGet("{id}/token-summary")]
    public async Task<IActionResult> GetTokenSummary(Guid id)
    {
        var summary = await _llmLogService.GetTokenSummaryAsync(id);
        return Ok(summary.ToTokenSummaryResponse());
    }

    [HttpGet("{id}/llm-calls")]
    public async Task<IActionResult> GetLlmCalls(Guid id)
    {
        var logs = await _llmLogService.GetTaskCallLogsAsync(id);
        return Ok(logs.Select(l => l.ToLlmCallLogResponse()));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _taskQueue.CancelAsync(id);
        return Ok(new { status = "cancelled" });
    }
}
