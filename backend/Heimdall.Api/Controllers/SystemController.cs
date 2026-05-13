using Heimdall.Api.Models;
using Heimdall.Api.Services.SystemInfo;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供系统基础信息与健康检查接口。
/// </summary>
[ApiController]
public sealed class SystemController : ControllerBase
{
    private readonly SystemInfoService _systemInfoService;

    /// <summary>
    /// 初始化系统控制器。
    /// </summary>
    public SystemController(SystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
    }

    /// <summary>
    /// 获取服务首页信息。
    /// </summary>
    [HttpGet("/")]
    public ActionResult<AppInfoResponse> GetIndex()
    {
        return Ok(_systemInfoService.BuildAppInfo());
    }

    /// <summary>
    /// 获取服务健康状态。
    /// </summary>
    [HttpGet("/health")]
    public ActionResult<HealthStatusResponse> GetHealth()
    {
        return Ok(_systemInfoService.BuildHealthStatus());
    }
}
