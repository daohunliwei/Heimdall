using System.Diagnostics;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskLlmService
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly ProviderRateLimiter _rateLimiter;
    private readonly LlmRetryPolicy _retryPolicy;
    private readonly ILogger<TaskLlmService> _logger;

    public TaskLlmService(
        ProviderRegistry providerRegistry,
        ProviderRateLimiter rateLimiter,
        LlmRetryPolicy retryPolicy,
        ILogger<TaskLlmService> logger)
    {
        _providerRegistry = providerRegistry;
        _rateLimiter = rateLimiter;
        _retryPolicy = retryPolicy;
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

        _logger.LogInformation("LLM 调用 Provider={Provider} Model={Model} PromptLen={Len}",
            resolvedProviderId, resolvedModel, prompt.Length);

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

        _logger.LogInformation(
            "LLM 调用完成 Provider={Provider} Model={Model} ElapsedMs={Ms} InputTokens={In} OutputTokens={Out} CacheHit={Cache} Estimated={Est}",
            resolvedProviderId, resolvedModel, response.LatencyMs,
            response.Usage.InputTokens, response.Usage.OutputTokens,
            response.Usage.CacheHitTokens, response.Usage.IsEstimated);

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
}
