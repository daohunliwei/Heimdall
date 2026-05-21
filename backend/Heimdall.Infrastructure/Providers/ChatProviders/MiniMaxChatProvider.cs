using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// MiniMax 聊天 Provider（OpenAI 兼容 Chat Completions 协议）。
/// </summary>
public sealed class MiniMaxChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 MiniMax 聊天 Provider。
    /// </summary>
    public MiniMaxChatProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId => "minimax";

    /// <summary>
    /// 发起带指标和缓存命中提取的聊天补全请求。
    /// </summary>
    public async Task<ChatCompletionResponse> GenerateWithMetricsAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var apiKey = _configuration["MINIMAX_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ChatCompletionResponse { Content = "MINIMAX_API_KEY not configured.", FinishReason = "error", LatencyMs = 0 };

        var endpoint = GetEndpoint();
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var messages = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = request.SystemPrompt });
        messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = request.Prompt });

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false
        };
        if (request.Temperature.HasValue) payload["temperature"] = Clamp01Exclusive(request.Temperature.Value);
        if (request.TopP.HasValue) payload["top_p"] = Clamp01Exclusive(request.TopP.Value);

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ChatCompletionResponse { Content = $"minimax API error ({(int)response.StatusCode}): {responseText}", FinishReason = "error", LatencyMs = 0 };

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        int cacheHitTokens = 0, inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct)) outputTokens = ct.GetInt32();
            if (usage.TryGetProperty("cache_read_input_tokens", out var cache)) cacheHitTokens = cache.GetInt32();
        }

        string content = string.Empty;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt))
                content = cnt.GetString() ?? string.Empty;
        }

        sw.Stop();
        return new ChatCompletionResponse
        {
            Content = content,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            FinishReason = "stop",
            Usage = new TokenUsage { InputTokens = inputTokens, OutputTokens = outputTokens, CacheHitTokens = cacheHitTokens }
        };
    }

    /// <summary>
    /// 发起聊天补全请求（简单版，回退到 GenerateWithMetricsAsync）。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var result = await GenerateWithMetricsAsync(request, cancellationToken);
        return result.Content;
    }

    /// <summary>
    /// 获取 MiniMax Chat Completions 端点。
    /// </summary>
    private string GetEndpoint()
    {
        var baseUrl = (_configuration["MINIMAX_BASE_URL"] ?? "https://api.minimaxi.com/v1").TrimEnd('/');
        return $"{baseUrl}/chat/completions";
    }

    /// <summary>
    /// 将参数限制在 (0, 1] 区间，避免服务端直接拒绝。
    /// </summary>
    private static double Clamp01Exclusive(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        if (value <= 0)
        {
            return 0.01;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}
