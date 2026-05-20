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

    /// <summary>
    /// GET /api/providers/metadata — 返回所有 Provider 的模型元数据（V7）。
    /// 包含 BillingType、MaxContextTokens、MaxOutputTokens、ContextFillRatio 等信息。
    /// </summary>
    [HttpGet("api/providers/metadata")]
    public IActionResult GetProviderMetadata()
    {
        var config = _configService.GetGeneratorConfig();
        var fillRatio = _configService.GetContextFillRatio();
        var pipelineVersion = _configService.GetWikiPipelineVersion();

        var result = new Dictionary<string, object>();
        foreach (var (providerKey, providerDef) in config.Providers)
        {
            var metadata = _configService.GetProviderModelMetadata(providerKey, providerDef.DefaultModel ?? "");
            result[providerKey] = new
            {
                defaultModel = providerDef.DefaultModel,
                billingType = metadata.BillingType.ToString(),
                maxContextTokens = metadata.MaxContextTokens,
                maxOutputTokens = metadata.MaxOutputTokens,
                contextFillRatio = fillRatio,
                effectiveBudget = (int)(metadata.MaxContextTokens * fillRatio) - metadata.MaxOutputTokens,
                models = providerDef.Models.Keys.ToList()
            };
        }

        return Ok(new
        {
            pipelineVersion,
            contextFillRatio = fillRatio,
            providers = result
        });
    }
}
