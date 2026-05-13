using Heimdall.Api.Mappings;
using Heimdall.Api.Models;
using Heimdall.Core.Interfaces.Repositories;
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

    public TasksAdminController(ITaskRepository taskRepo, TaskQueueService taskQueue)
    {
        _taskRepo = taskRepo;
        _taskQueue = taskQueue;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? taskType = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var (items, total) = await _taskRepo.GetAllAsync(status, taskType, null, offset, limit);
        return Ok(new TaskListResponse
        {
            Tasks = items.Select(t => t.ToTaskStatusResponse()).ToList(),
            Total = total
        });
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

        // 重置状态并重新入队
        await _taskRepo.UpdateStatusAsync(id, "pending", progressPercent: 0, errorMessage: null);
        return Ok(new { status = "pending" });
    }
}
