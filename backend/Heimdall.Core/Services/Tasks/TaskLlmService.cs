using System.Diagnostics;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// LLM 调用服务 — 基于 MEAI IChatClient 封装调用、日志与成本估算。
/// </summary>
public sealed class TaskLlmService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<TaskLlmService> _logger;

    public TaskLlmService(
        IServiceProvider serviceProvider,
        HeimdallConfigService configService,
        ILogger<TaskLlmService> logger)
    {
        _serviceProvider = serviceProvider;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 生成文本（非流式）。
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string provider, string? model, string? customModel, string prompt,
        CancellationToken ct, string? systemPrompt = null)
    {
        var response = await GenerateWithMetricsAsync(provider, model, customModel, prompt, ct, systemPrompt);
        return response.Messages.LastOrDefault()?.Text ?? string.Empty;
    }

    /// <summary>
    /// 带完整指标的 LLM 调用——返回 ChatResponse（含 UsageDetails）。
    /// </summary>
    public async Task<ChatResponse> GenerateWithMetricsAsync(
        string provider, string? model, string? customModel, string prompt,
        CancellationToken ct, string? systemPrompt = null,
        IList<AITool>? tools = null)
    {
        var providerId = !string.IsNullOrWhiteSpace(provider) ? provider : "ollama";
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model
            : !string.IsNullOrWhiteSpace(customModel) ? customModel
            : null;

        if (string.IsNullOrWhiteSpace(effectiveModel))
        {
            throw new InvalidOperationException(
                $"无法解析 Provider='{providerId}' 的模型。请在请求中指定 model 参数。");
        }

        var chatClient = _serviceProvider.GetKeyedService<IChatClient>(providerId);
        if (chatClient is null)
        {
            throw new InvalidOperationException(
                $"未找到 Provider '{providerId}' 的 IChatClient 注册");
        }
        var estimatedPromptTokens = TokenCounter.EstimateTokenCount(prompt);

        _logger.LogInformation(
            "[LLM] 调用开始 Provider={Provider} Model={Model} PromptTokens(est)={PromptTokens}",
            providerId, effectiveModel, estimatedPromptTokens);

        var sw = Stopwatch.StartNew();

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }
        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var options = new ChatOptions
        {
            ModelId = effectiveModel,
            MaxOutputTokens = 8192,
        };

        if (tools is { Count: > 0 })
        {
            options.Tools = tools;
        }

        try
        {
            var response = await chatClient.GetResponseAsync(messages, options, ct);
            sw.Stop();

            var usage = response.Usage ?? new UsageDetails();
            var estimatedCost = EstimateCallCost(providerId,
                (int)usage.InputTokenCount, (int)usage.OutputTokenCount);

            _logger.LogInformation(
                "[LLM] 调用完成 Provider={Provider} Model={Model} Latency={Ms}ms InputTokens={In} OutputTokens={Out} Cost≈${Cost:F4}",
                providerId, effectiveModel, sw.ElapsedMilliseconds,
                usage.InputTokenCount, usage.OutputTokenCount, estimatedCost);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[LLM] 调用失败 Provider={Provider} Model={Model} Latency={Ms}ms",
                providerId, effectiveModel, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public (string providerId, string model) ResolveTarget(string? provider, string? model, string? customModel)
    {
        var providerId = !string.IsNullOrWhiteSpace(provider) ? provider : "ollama";
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model
            : !string.IsNullOrWhiteSpace(customModel) ? customModel
            : string.Empty;
        return (providerId, effectiveModel);
    }

    private static decimal EstimateCallCost(string provider, long? inputTokens, long? outputTokens)
    {
        if (provider.Contains("ollama", StringComparison.OrdinalIgnoreCase))
            return 0m;

        var inputCostPerMTok = provider.Contains("openai", StringComparison.OrdinalIgnoreCase) ? 2.5m
            : provider.Contains("google", StringComparison.OrdinalIgnoreCase) ? 1.25m
            : 2.0m;
        var outputCostPerMTok = provider.Contains("openai", StringComparison.OrdinalIgnoreCase) ? 10.0m
            : provider.Contains("google", StringComparison.OrdinalIgnoreCase) ? 5.0m
            : 8.0m;

        return ((inputTokens ?? 0) * inputCostPerMTok + (outputTokens ?? 0) * outputCostPerMTok) / 1_000_000m;
    }
}
