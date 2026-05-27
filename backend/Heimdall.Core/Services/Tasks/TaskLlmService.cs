using System.Diagnostics;
using System.Text;
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
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var response = await GenerateWithMetricsAsync(
            provider,
            model,
            customModel,
            messages,
            new ChatOptions(),
            ct);
        return response.Messages.LastOrDefault()?.Text ?? string.Empty;
    }

    /// <summary>
    /// 基于结构化消息生成文本
    /// 该入口是当前推荐主路径
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string provider,
        string? model,
        string? customModel,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        var response = await GenerateWithMetricsAsync(provider, model, customModel, messages, options, ct);
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
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var options = new ChatOptions
        {
            MaxOutputTokens = 8192
        };

        if (tools is { Count: > 0 })
        {
            options.Tools = tools;
        }

        return await GenerateWithMetricsAsync(provider, model, customModel, messages, options, ct);
    }

    /// <summary>
    /// 带完整指标的结构化消息调用
    /// 统一由 ChatOptions 承载模型与工具参数
    /// </summary>
    public async Task<ChatResponse> GenerateWithMetricsAsync(
        string provider,
        string? model,
        string? customModel,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        var providerId = !string.IsNullOrWhiteSpace(provider) ? provider : "ollama";
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model
            : !string.IsNullOrWhiteSpace(customModel) ? customModel
            : ResolveDefaultModel(providerId);

        if (string.IsNullOrWhiteSpace(effectiveModel))
        {
            throw new InvalidOperationException(
                $"无法解析 Provider='{providerId}' 的模型。请在请求中指定 model 参数。");
        }

        var chatClient = _serviceProvider.GetRequiredKeyedService<IChatClient>(providerId);
        var estimatedPromptTokens = EstimateMessageTokens(messages);

        _logger.LogInformation(
            "[LLM] 调用开始 Provider={Provider} Model={Model} PromptTokens(est)={PromptTokens}",
            providerId, effectiveModel, estimatedPromptTokens);

        var sw = Stopwatch.StartNew();
        var effectiveOptions = options ?? new ChatOptions();
        effectiveOptions.ModelId = effectiveModel;
        effectiveOptions.MaxOutputTokens ??= 8192;

        try
        {
            var response = await chatClient.GetResponseAsync(messages, effectiveOptions, ct);
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
            : ResolveDefaultModel(providerId) ?? string.Empty;
        return (providerId, effectiveModel);
    }

    /// <summary>
    /// 从生成器配置中解析 Provider 默认模型
    /// 未配置时返回 null，由调用方决定是否继续抛错
    /// </summary>
    private string? ResolveDefaultModel(string providerId)
    {
        var generatorConfig = _configService.GetGeneratorConfig();
        return generatorConfig.Providers.TryGetValue(providerId, out var definition)
            ? definition.DefaultModel
            : null;
    }

    /// <summary>
    /// 估算结构化消息的输入 Token
    /// 仅用于日志观测
    /// </summary>
    private static int EstimateMessageTokens(IEnumerable<ChatMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                builder.AppendLine(message.Text);
            }
        }

        return TokenCounter.EstimateTokenCount(builder.ToString());
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
