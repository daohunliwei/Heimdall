using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
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
    private readonly ISystemSettingRepository _settingRepo;
    private readonly Heimdall.Core.Services.Prompt.PromptSeedData? _promptSeedData;

    public AdminController(IProviderMetadataRepository metadataRepo, HeimdallConfigService configService,
        ITaskRepository taskRepo, ILlmObservabilityService observability,
        ISystemSettingRepository settingRepo,
        Heimdall.Core.Services.Prompt.PromptSeedData? promptSeedData = null)
    {
        _metadataRepo = metadataRepo;
        _configService = configService;
        _taskRepo = taskRepo;
        _observability = observability;
        _settingRepo = settingRepo;
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

    // ── Debug Config ──

    [HttpGet("debug-config")]
    public async Task<IActionResult> GetDebugConfig()
    {
        var enabled = await _settingRepo.GetByKeyAsync("DebugMode.Enabled");
        var maxPages = await _settingRepo.GetByKeyAsync("DebugMode.MaxPages");
        return Ok(new
        {
            enabled = enabled?.Value == "true",
            maxDebugPages = int.TryParse(maxPages?.Value, out var mp) ? mp : 5
        });
    }

    [HttpPut("debug-config")]
    public async Task<IActionResult> UpdateDebugConfig([FromBody] DebugConfigRequest request)
    {
        if (request.MaxDebugPages < 1 || request.MaxDebugPages > 20)
            return BadRequest(new { error = "最大调试页数必须为 1-20 之间的正整数" });

        await _settingRepo.SetAsync("DebugMode.Enabled", request.Enabled.ToString().ToLower());
        await _settingRepo.SetAsync("DebugMode.MaxPages", request.MaxDebugPages.ToString());
        return Ok(new { enabled = request.Enabled, maxDebugPages = request.MaxDebugPages });
    }

    // ── Provider Status ──

    [HttpGet("provider-status")]
    public async Task<IActionResult> GetProviderStatus()
    {
        var config = _configService.GetGeneratorConfig();
        var metadata = await _metadataRepo.GetAllAsync();
        var result = new List<object>();

        foreach (var (providerKey, providerConfig) in config.Providers)
        {
            var dbModels = metadata.Where(m => m.ProviderKey == providerKey).ToList();
            var hasApiKey = !string.IsNullOrEmpty(GetProviderApiKey(providerKey));
            // 合并数据库元数据与 generator.json 默认模型列表
            var mergedModels = new List<object>();
            foreach (var m in dbModels)
            {
                mergedModels.Add(new
                {
                    modelName = m.ModelName,
                    billingType = m.BillingType,
                    maxContextTokens = m.MaxContextTokens,
                    maxOutputTokens = m.MaxOutputTokens,
                    contextFillRatio = m.ContextFillRatio,
                    contextWarningThreshold = m.ContextWarningThreshold,
                    supportsCaching = m.SupportsCaching
                });
            }
            // 补充 generator.json 中有但数据库中没有的模型
            var dbModelNames = new HashSet<string>(dbModels.Select(m => m.ModelName), StringComparer.OrdinalIgnoreCase);
            foreach (var (modelName, modelParams) in providerConfig.Models)
            {
                if (!dbModelNames.Contains(modelName))
                {
                    mergedModels.Add(new
                    {
                        modelName,
                        billingType = (providerConfig.Metadata?.BillingType ?? BillingType.TokenPlan).ToString(),
                        maxContextTokens = providerConfig.Metadata?.MaxContextTokens ?? 128000,
                        maxOutputTokens = providerConfig.Metadata?.MaxOutputTokens ?? 8192,
                        contextFillRatio = providerConfig.Metadata?.ContextFillRatio ?? 0.65,
                        contextWarningThreshold = providerConfig.Metadata?.ContextWarningThreshold ?? 0.90,
                        supportsCaching = providerConfig.Metadata?.SupportsCaching ?? false
                    });
                }
            }
            result.Add(new
            {
                provider = providerKey,
                displayName = providerKey,
                hasApiKey,
                status = hasApiKey ? "configured" : "no_key",
                modelCount = mergedModels.Count,
                models = mergedModels
            });
        }
        return Ok(result);
    }

    // ── System Config ──

    [HttpGet("system-config")]
    public IActionResult GetSystemConfig()
    {
        var maskSensitive = (string key, string? value) =>
        {
            if (string.IsNullOrEmpty(value)) return "—";
            if (value.Length <= 6) return "***";
            return value[..3] + "***" + value[^3..];
        };

        var serviceConfig = new Dictionary<string, object>
        {
            ["认证模式"] = new { value = _configService.GetAuthMode(), source = ResolveSource("HEIMDALL_AUTH_MODE") },
            ["开放注册"] = new { value = _configService.GetRegistrationOpen() ? "是" : "否", source = ResolveSource("HEIMDALL_REGISTRATION_OPEN") },
            ["管线版本"] = new { value = "8 阶段（当前）", source = "default" },
            ["默认 Provider"] = new { value = _configService.GetDefaultProvider(), source = ResolveSource("HEIMDALL_DEFAULT_PROVIDER") },
            ["嵌入器类型"] = new { value = "当前未启用独立嵌入链路", source = ResolveSource("HEIMDALL_EMBEDDER_TYPE") },
            ["上下文填充比例"] = new { value = $"{(_configService.GetContextFillRatio() * 100):F0}%", source = "default" }
        };

        var resourceConfig = new Dictionary<string, object>
        {
            ["数据目录"] = new { value = Environment.GetEnvironmentVariable("HEIMDALL_DATA_DIR") ?? "data/（默认）", source = ResolveSource("HEIMDALL_DATA_DIR") },
            ["暂存目录"] = new { value = Environment.GetEnvironmentVariable("HEIMDALL_STORAGE_DIR") ?? "storage/（默认）", source = ResolveSource("HEIMDALL_STORAGE_DIR") },
            ["HTTP 超时"] = new { value = (Environment.GetEnvironmentVariable("HEIMDALL_HTTP_TIMEOUT_MINUTES") ?? "180") + " 分钟", source = ResolveSource("HEIMDALL_HTTP_TIMEOUT_MINUTES") },
            ["Wiki 任务超时"] = new { value = (Environment.GetEnvironmentVariable("HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES") ?? "180") + " 分钟", source = ResolveSource("HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES") }
        };

        var keyNames = new[] { "OPENAI", "GOOGLE", "MINIMAX", "DASHSCOPE", "DEEPSEEK", "OPENROUTER", "AZURE_OPENAI", "AWS" };
        var providerKeyStatus = keyNames.Select(name => new
        {
            provider = name,
            envVar = $"{name}_API_KEY",
            isSet = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable($"{name}_API_KEY")),
            maskedValue = maskSensitive($"{name}_API_KEY", Environment.GetEnvironmentVariable($"{name}_API_KEY"))
        }).ToList();

        providerKeyStatus.Add(new
        {
            provider = "Ollama",
            envVar = "OLLAMA_HOST",
            isSet = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_HOST")),
            maskedValue = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434（默认）"
        });

        return Ok(new
        {
            serviceConfig,
            resourceConfig,
            providerKeyStatus
        });
    }

    private static string ResolveSource(string envKey)
    {
        return Environment.GetEnvironmentVariable(envKey) is not null ? "env" : "default";
    }

    private static string? GetProviderApiKey(string providerKey)
    {
        return providerKey.ToLower() switch
        {
            "openai" => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            "google" => Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
            "minimax" => Environment.GetEnvironmentVariable("MINIMAX_API_KEY"),
            "dashscope" => Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY"),
            "deepseek" => Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            "openrouter" => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            "azure" => Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),
            "bedrock" or "aws" => Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            "ollama" => Environment.GetEnvironmentVariable("OLLAMA_HOST")
                ?? Environment.GetEnvironmentVariable("HEIMDALL_OLLAMA_CHAT_HOST"),
            _ => null
        };
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

public class DebugConfigRequest
{
    public bool Enabled { get; set; }
    public int MaxDebugPages { get; set; } = 5;
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
