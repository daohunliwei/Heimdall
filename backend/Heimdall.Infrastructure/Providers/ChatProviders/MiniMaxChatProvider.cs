using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// MiniMax 聊天 Provider（OpenAI 兼容 Chat Completions 协议，非流式）。
/// </summary>
public sealed class MiniMaxChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MiniMaxChatProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public string ProviderId => "minimax";

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
            ["stream"] = false,
            ["max_completion_tokens"] = 196608,
            ["temperature"] = request.Temperature ?? 0.7
        };
        if (request.TopP.HasValue) payload["top_p"] = Clamp01Exclusive(request.TopP.Value);

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ChatCompletionResponse { Content = $"minimax API error ({(int)response.StatusCode}): {responseText}", FinishReason = "error", LatencyMs = 0 };

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        int inputTokens = 0, outputTokens = 0, cacheHitTokens = 0;
        string content = string.Empty;
        string? finishReason = null;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt))
                content = cnt.GetString() ?? string.Empty;
            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                finishReason = fr.GetString();
        }

        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct)) outputTokens = ct.GetInt32();
            if (usage.TryGetProperty("cache_read_input_tokens", out var cache)) cacheHitTokens = cache.GetInt32();
            else if (usage.TryGetProperty("prompt_tokens_details", out var details) &&
                     details.TryGetProperty("cached_tokens", out var cached)) cacheHitTokens = cached.GetInt32();
        }

        sw.Stop();
        return new ChatCompletionResponse
        {
            Content = content,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            FinishReason = finishReason ?? "stop",
            Usage = new TokenUsage { InputTokens = inputTokens, OutputTokens = outputTokens, CacheHitTokens = cacheHitTokens }
        };
    }

    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var result = await GenerateWithMetricsAsync(request, cancellationToken);
        return result.Content;
    }

    private string GetEndpoint()
    {
        var baseUrl = (_configuration["MINIMAX_BASE_URL"] ?? "https://api.minimaxi.com/v1").TrimEnd('/');
        return $"{baseUrl}/chat/completions";
    }

    private static double Clamp01Exclusive(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 1.0;
        if (value <= 0) return 0.01;
        if (value > 1.0) return 1.0;
        return value;
    }
}
