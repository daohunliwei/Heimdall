using Heimdall.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/logging")]
public class LoggingController : ControllerBase
{
    private readonly LogCategoryFilter _filter;

    public LoggingController(LogCategoryFilter filter)
    {
        _filter = filter;
    }

    /// <summary>
    /// GET /api/admin/logging/status — 返回当前日志过滤状态
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            showSql = _filter.ShowSqlCommands,
            showEfCore = _filter.ShowEfCore,
            showStructuredProgress = _filter.ShowStructuredProgress
        });
    }

    /// <summary>
    /// POST /api/admin/logging/filter — 动态切换日志过滤
    /// </summary>
    [HttpPost("filter")]
    public IActionResult SetFilter([FromBody] LogFilterRequest request)
    {
        if (request.ShowSql.HasValue)
            _filter.ShowSqlCommands = request.ShowSql.Value;

        if (request.ShowEfCore.HasValue)
            _filter.ShowEfCore = request.ShowEfCore.Value;

        if (request.ShowStructuredProgress.HasValue)
            _filter.ShowStructuredProgress = request.ShowStructuredProgress.Value;

        return Ok(new
        {
            showSql = _filter.ShowSqlCommands,
            showEfCore = _filter.ShowEfCore,
            showStructuredProgress = _filter.ShowStructuredProgress
        });
    }
}

public class LogFilterRequest
{
    public bool? ShowSql { get; set; }
    public bool? ShowEfCore { get; set; }
    public bool? ShowStructuredProgress { get; set; }
}
