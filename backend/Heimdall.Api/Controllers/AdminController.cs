using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly IProviderMetadataRepository _metadataRepo;
    private readonly HeimdallConfigService _configService;
    private readonly ITaskRepository _taskRepo;
    private readonly ILlmObservabilityService _observability;
    private readonly Heimdall.Core.Services.Prompt.PromptSeedData? _promptSeedData;

    public AdminController(IProviderMetadataRepository metadataRepo, HeimdallConfigService configService,
        ITaskRepository taskRepo, ILlmObservabilityService observability,
        Heimdall.Core.Services.Prompt.PromptSeedData? promptSeedData = null)
    {
        _metadataRepo = metadataRepo;
        _configService = configService;
        _taskRepo = taskRepo;
        _observability = observability;
        _promptSeedData = promptSeedData;
    }

    // ── Provider Metadata ──

    [HttpGet("provider-metadata")]
    public async Task<IActionResult> GetAllMetadata(CancellationToken ct)
    {
        var entities = await _metadataRepo.GetAllAsync(ct);
        var result = entities
            .GroupBy(e => e.ProviderKey)
            .ToDictionary(g => g.Key, g => g.Select(e => new
            {
                modelName = e.ModelName,
                billingType = e.BillingType,
                maxContextTokens = e.MaxContextTokens,
                maxOutputTokens = e.MaxOutputTokens,
                rateLimitPerMinute = e.RateLimitPerMinute,
                inputTokenPrice = e.InputTokenPrice,
                outputTokenPrice = e.OutputTokenPrice,
                callPrice = e.CallPrice,
                supportsCaching = e.SupportsCaching,
                contextFillRatio = e.ContextFillRatio,
                contextWarningThreshold = e.ContextWarningThreshold,
                updatedAt = e.UpdatedAt
            }));
        return Ok(result);
    }

    [HttpPut("provider-metadata/{provider}/{model}")]
    public async Task<IActionResult> UpsertMetadata(string provider, string model,
        [FromBody] UpsertProviderMetadataRequest request, CancellationToken ct)
    {
        var entity = new ProviderModelMetadataEntity
        {
            ProviderKey = provider,
            ModelName = model,
            BillingType = request.BillingType ?? "TokenPlan",
            MaxContextTokens = request.MaxContextTokens ?? 128000,
            MaxOutputTokens = request.MaxOutputTokens ?? 8192,
            RateLimitPerMinute = request.RateLimitPerMinute,
            InputTokenPrice = request.InputTokenPrice,
            OutputTokenPrice = request.OutputTokenPrice,
            CallPrice = request.CallPrice,
            SupportsCaching = request.SupportsCaching ?? false,
            ContextFillRatio = request.ContextFillRatio ?? 0.65,
            ContextWarningThreshold = request.ContextWarningThreshold ?? 0.90
        };
        await _metadataRepo.UpsertAsync(entity, ct);
        _configService.InvalidateMetadataCache();
        return Ok(new { message = "保存成功" });
    }

    [HttpDelete("provider-metadata/{provider}/{model}")]
    public async Task<IActionResult> DeleteMetadata(string provider, string model, CancellationToken ct)
    {
        await _metadataRepo.DeleteAsync(provider, model, ct);
        _configService.InvalidateMetadataCache();
        return Ok(new { message = "已删除，将回退到默认值" });
    }

    // ── System Info ──

    [HttpGet("system-info")]
    public IActionResult GetSystemInfo()
    {
        var config = _configService.GetGeneratorConfig();
        return Ok(new
        {
            defaultProvider = _configService.GetDefaultProvider(),
            embedderType = _configService.GetEmbedderType(),
            contextFillRatio = _configService.GetContextFillRatio(),
            providers = config.Providers.Keys.ToList(),
            pipeline_10_stage = true,
            auth_mode = _configService.GetAuthMode(),
            registration_open = _configService.GetRegistrationOpen()
        });
    }

    // ── Prompt Reset ──

    [HttpPost("prompt-templates/reset-defaults")]
    public async Task<IActionResult> ResetPromptDefaults()
    {
        if (_promptSeedData is null) return StatusCode(500, new { error = "PromptSeedData 未注册" });
        await _promptSeedData.ResetAllToDefaultsAsync();
        return Ok(new { message = "所有系统提示词已重置为代码默认版本" });
    }
}

public class UpsertProviderMetadataRequest
{
    public string? BillingType { get; set; }
    public int? MaxContextTokens { get; set; }
    public int? MaxOutputTokens { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public decimal? InputTokenPrice { get; set; }
    public decimal? OutputTokenPrice { get; set; }
    public decimal? CallPrice { get; set; }
    public bool? SupportsCaching { get; set; }
    public double? ContextFillRatio { get; set; }
    public double? ContextWarningThreshold { get; set; }
}
