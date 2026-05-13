using Heimdall.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
public class ConfigurationController : ControllerBase
{
    private readonly HeimdallConfigService _configService;

    public ConfigurationController(HeimdallConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// GET /models/config — 返回 Provider/Model 配置供前端选择。
    /// </summary>
    [HttpGet("models/config")]
    public IActionResult GetModelConfig()
    {
        var response = _configService.BuildModelConfigResponse();
        return Ok(response);
    }
}
