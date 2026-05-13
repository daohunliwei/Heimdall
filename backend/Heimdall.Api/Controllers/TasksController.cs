using Heimdall.Api.Models;
using Heimdall.Api.Services.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供后端主导的 Wiki、Ask、Slides、Workshop 任务接口。
/// </summary>
[ApiController]
[Route("tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly AskTaskService _askTaskService;
    private readonly ILogger<TasksController> _logger;
    private readonly SlidesTaskService _slidesTaskService;
    private readonly WikiTaskService _wikiTaskService;
    private readonly WorkshopTaskService _workshopTaskService;

    /// <summary>
    /// 初始化任务控制器。
    /// </summary>
    public TasksController(
        AskTaskService askTaskService,
        ILogger<TasksController> logger,
        SlidesTaskService slidesTaskService,
        WikiTaskService wikiTaskService,
        WorkshopTaskService workshopTaskService)
    {
        _askTaskService = askTaskService;
        _logger = logger;
        _slidesTaskService = slidesTaskService;
        _wikiTaskService = wikiTaskService;
        _workshopTaskService = workshopTaskService;
    }

    /// <summary>
    /// 生成 Wiki。
    /// </summary>
    [HttpPost("wiki")]
    public async Task<ActionResult<WikiTaskResponse>> GenerateWikiAsync([FromBody] WikiTaskRequest request, CancellationToken cancellationToken)
    {
        var requestId = HttpContext.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();
        if (HttpContext.RequestAborted.CanBeCanceled)
        {
            HttpContext.RequestAborted.Register(() =>
                _logger.LogWarning(
                    "检测到前端请求已断开 RequestId={RequestId} Path={Path}，后端将继续执行 Wiki 任务",
                    requestId,
                    HttpContext.Request.Path));
        }

        try
        {
            var response = await _wikiTaskService.GenerateAsync(request, cancellationToken);
            _logger.LogInformation(
                "Wiki 生成完成 RequestId={RequestId} RequestAborted={RequestAborted} ElapsedMs={ElapsedMs} GeneratedPages={GeneratedPages}",
                requestId,
                HttpContext.RequestAborted.IsCancellationRequested,
                stopwatch.ElapsedMilliseconds,
                response.GeneratedPages.Count);
            return Ok(response);
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(
                exception,
                "Wiki 生成被取消 RequestId={RequestId} RequestAborted={RequestAborted} CallerCancellation={CallerCancellation} ElapsedMs={ElapsedMs}",
                requestId,
                HttpContext.RequestAborted.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                stopwatch.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new TaskErrorResponse
            {
                Error = "Wiki 生成被取消",
                Details = HttpContext.RequestAborted.IsCancellationRequested
                    ? "前端请求已断开，但后端应已记录继续执行日志；如未完成，请检查服务是否重启或命中总任务超时"
                    : exception.Message,
                RequestId = requestId
            });
        }
        catch (Exception exception)
        {
            return BuildTaskError("Wiki 生成失败", exception);
        }
    }

    /// <summary>
    /// 执行 Ask 任务。
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AskTaskResponse>> GenerateAskAsync([FromBody] AskTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _askTaskService.GenerateAsync(request, cancellationToken));
        }
        catch (Exception exception)
        {
            return BuildTaskError("Ask 任务失败", exception);
        }
    }

    /// <summary>
    /// 生成 Slides。
    /// </summary>
    [HttpPost("slides")]
    public async Task<ActionResult<SlidesTaskResponse>> GenerateSlidesAsync([FromBody] SlidesTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _slidesTaskService.GenerateAsync(request, cancellationToken));
        }
        catch (Exception exception)
        {
            return BuildTaskError("Slides 生成失败", exception);
        }
    }

    /// <summary>
    /// 生成 Workshop。
    /// </summary>
    [HttpPost("workshop")]
    public async Task<ActionResult<WorkshopTaskResponse>> GenerateWorkshopAsync([FromBody] WorkshopTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _workshopTaskService.GenerateAsync(request, cancellationToken));
        }
        catch (Exception exception)
        {
            return BuildTaskError("Workshop 生成失败", exception);
        }
    }

    private ObjectResult BuildTaskError(string summary, Exception exception)
    {
        var requestId = HttpContext.TraceIdentifier;
        _logger.LogError(exception, "{Summary} RequestId={RequestId}", summary, requestId);

        return StatusCode(StatusCodes.Status500InternalServerError, new TaskErrorResponse
        {
            Error = summary,
            Details = exception.Message,
            RequestId = requestId
        });
    }
}
