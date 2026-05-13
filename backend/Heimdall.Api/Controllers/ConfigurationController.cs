using Heimdall.Api.Models;
using Heimdall.Api.Services.Auth;
using Heimdall.Api.Services.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供语言、鉴权与模型配置接口。
/// </summary>
[ApiController]
[Route("")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly HeimdallConfigService _configService;

    /// <summary>
    /// 初始化配置控制器。
    /// </summary>
    public ConfigurationController(AuthorizationService authorizationService, HeimdallConfigService configService)
    {
        _authorizationService = authorizationService;
        _configService = configService;
    }

    /// <summary>
    /// 获取语言配置。
    /// </summary>
    [HttpGet("lang/config")]
    public ActionResult<LanguageConfig> GetLanguageConfig()
    {
        return Ok(_configService.GetLanguageConfig());
    }

    /// <summary>
    /// 获取鉴权状态。
    /// </summary>
    [HttpGet("auth/status")]
    public ActionResult<AuthStatusResponse> GetAuthorizationStatus()
    {
        return Ok(_authorizationService.BuildStatusResponse());
    }

    /// <summary>
    /// 校验授权码是否有效。
    /// </summary>
    [HttpPost("auth/validate")]
    public ActionResult<AuthorizationValidationResponse> ValidateAuthorization([FromBody] AuthorizationRequest request)
    {
        return Ok(_authorizationService.Validate(request));
    }

    /// <summary>
    /// 获取模型配置。
    /// </summary>
    [HttpGet("models/config")]
    public ActionResult<ModelConfigResponse> GetModelConfig()
    {
        return Ok(_configService.BuildModelConfigResponse());
    }
}
