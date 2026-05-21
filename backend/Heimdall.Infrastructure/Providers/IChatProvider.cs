using Heimdall.Infrastructure.Models;

namespace Heimdall.Infrastructure.Providers;

public interface IChatProvider
{
    string ProviderId { get; }
    Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// V7: 带结构化响应的 LLM 调用——返回 Token 用量和延迟信息。
    /// 默认实现回退到 GenerateAsync 并使用估算值填充 usage。
    /// </summary>
    Task<ChatCompletionResponse> GenerateWithMetricsAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        return DefaultGenerateWithMetricsAsync(this, request, cancellationToken);
    }

    /// <summary>
    /// 默认实现：调用 GenerateAsync 并用 TokenCounter 估算 usage。
    /// </summary>
    private static async Task<ChatCompletionResponse> DefaultGenerateWithMetricsAsync(
        IChatProvider provider, ProviderChatRequest request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var content = await provider.GenerateAsync(request, ct);
        sw.Stop();

        return new ChatCompletionResponse
        {
            Content = content,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            FinishReason = "stop",
            Usage = new TokenUsage
            {
                InputTokens = Utilities.TokenCounter.EstimateTokenCount(request.Prompt)
                    + Utilities.TokenCounter.EstimateTokenCount(request.SystemPrompt),
                OutputTokens = Utilities.TokenCounter.EstimateTokenCount(content),
                IsEstimated = true
            }
        };
    }
}
