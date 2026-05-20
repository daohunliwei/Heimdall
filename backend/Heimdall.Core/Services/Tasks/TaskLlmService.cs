using System.Diagnostics;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Services;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskLlmService
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly ProviderRateLimiter _rateLimiter;
    private readonly LlmRetryPolicy _retryPolicy;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<TaskLlmService> _logger;

    public TaskLlmService(
        ProviderRegistry providerRegistry,
        ProviderRateLimiter rateLimiter,
        LlmRetryPolicy retryPolicy,
        HeimdallConfigService configService,
        ILogger<TaskLlmService> logger)
    {
        _providerRegistry = providerRegistry;
        _rateLimiter = rateLimiter;
        _retryPolicy = retryPolicy;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 生成文本（兼容旧接口）。
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string provider, string? model, string? customModel, string prompt,
        CancellationToken ct, string? systemPrompt = null)
    {
        var response = await GenerateWithMetricsAsync(provider, model, customModel, prompt, ct, systemPrompt);
        return response.Content;
    }

    /// <summary>
    /// V7: 带完整指标的 LLM 调用——返回 Token 用量、延迟等元数据。
    /// </summary>
    public async Task<ChatCompletionResponse> GenerateWithMetricsAsync(
        string provider, string? model, string? customModel, string prompt,
        CancellationToken ct, string? systemPrompt = null)
    {
        var effectiveProvider = !string.IsNullOrWhiteSpace(provider) ? provider : "ollama";
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model
            : !string.IsNullOrWhiteSpace(customModel) ? customModel
            : null;

        var request = new ChatCompletionRequest
        {
            Provider = effectiveProvider,
            Model = effectiveModel ?? string.Empty,
            CustomModel = customModel,
            Messages = [new ChatMessage { Role = "user", Content = prompt }]
        };

        var (resolvedProviderId, resolvedModel, parameters, chatProvider) = _providerRegistry.ResolveChatProvider(request);

        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            throw new InvalidOperationException(
                $"无法解析 Provider='{resolvedProviderId}' 的模型。请在请求中指定 model 参数，或在 generator.json 中配置该 Provider 的默认模型。");
        }

        _logger.LogInformation(
            "[LLM] 调用开始 Provider={Provider} Model={Model} BillingType={Billing} PromptTokens(est)={PromptTokens} Strategy={Strategy}",
            resolvedProviderId, resolvedModel,
            _configService.GetProviderModelMetadata(effectiveProvider, effectiveModel ?? "").BillingType,
            TokenCounter.EstimateTokenCount(prompt),
            _configService.GetProviderModelMetadata(effectiveProvider, effectiveModel ?? "").BillingType == BillingType.CodingPlan ? "BatchMerge" : "PerItem");

        // 速率限制等待
        await _rateLimiter.AcquireAsync(resolvedProviderId, resolvedModel, ct);

        // 带重试的调用
        var response = await _retryPolicy.ExecuteAsync(async token =>
        {
            return await chatProvider.GenerateWithMetricsAsync(new ProviderChatRequest
            {
                ProviderId = resolvedProviderId,
                Model = resolvedModel,
                Prompt = prompt,
                SystemPrompt = systemPrompt,
                Temperature = parameters.Temperature,
                TopP = parameters.TopP,
                TopK = parameters.TopK,
                Options = parameters.Options
            }, token);
        }, $"GenerateText:{resolvedProviderId}/{resolvedModel}", ct);

        // V7: 增强调用后日志——Token 消耗、缓存命中、延迟、估算成本
        var estimatedCost = EstimateCallCost(resolvedProviderId, response.Usage);
        _logger.LogInformation(
            "[LLM] 调用完成 Provider={Provider} Model={Model} Latency={Ms}ms InputTokens={In} OutputTokens={Out} CacheHit={Cache} Estimated={Est} Cost≈${Cost:F4}",
            resolvedProviderId, resolvedModel, response.LatencyMs,
            response.Usage.InputTokens, response.Usage.OutputTokens,
            response.Usage.CacheHitTokens, response.Usage.IsEstimated, estimatedCost);

        return response;
    }

    public (string providerId, string model) ResolveTarget(string? provider, string? model, string? customModel)
    {
        var request = new ChatCompletionRequest
        {
            Provider = provider,
            Model = model ?? customModel ?? string.Empty,
            CustomModel = customModel
        };
        var (pid, m, _, _) = _providerRegistry.ResolveChatProvider(request);
        return (pid, m);
    }

    /// <summary>
    /// 简单成本估算（基于通用 Token 定价），用于日志参考。
    /// Ollama 等本地模型返回 0。
    /// </summary>
    private static decimal EstimateCallCost(string provider, TokenUsage usage)
    {
        // 通用估算: OpenAI GPT-4o 级别 $2.5/MTok input, $10/MTok output
        // 本地 Provider 免费
        if (provider.Contains("ollama", StringComparison.OrdinalIgnoreCase))
            return 0m;

        var inputCostPerMTok = provider.Contains("openai", StringComparison.OrdinalIgnoreCase) ? 2.5m
            : provider.Contains("google", StringComparison.OrdinalIgnoreCase) ? 1.25m
            : 2.0m;
        var outputCostPerMTok = provider.Contains("openai", StringComparison.OrdinalIgnoreCase) ? 10.0m
            : provider.Contains("google", StringComparison.OrdinalIgnoreCase) ? 5.0m
            : 8.0m;

        return (usage.InputTokens * inputCostPerMTok + usage.OutputTokens * outputCostPerMTok) / 1_000_000m;
    }
}
