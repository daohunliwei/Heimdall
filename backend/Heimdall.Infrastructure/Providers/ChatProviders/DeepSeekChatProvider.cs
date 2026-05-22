using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// DeepSeek Chat Provider，支持 reasoning_content 和 thinking 配置。
/// API 文档：https://api-docs.deepseek.com/zh-cn/api/create-chat-completion
/// </summary>
public sealed class DeepSeekChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public DeepSeekChatProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public string ProviderId => "deepseek";

    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var result = await GenerateWithMetricsAsync(request, cancellationToken);
        return result.Content;
    }

    public async Task<ChatCompletionResponse> GenerateWithMetricsAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var apiKey = _configuration["DEEPSEEK_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ChatCompletionResponse { Content = "DEEPSEEK_API_KEY not configured.", FinishReason = "error", LatencyMs = 0 };

        var endpoint = GetEndpoint();
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var messages = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = request.SystemPrompt });
        messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = request.Prompt });

        var maxTokens = request.MaxOutputTokens > 0 ? request.MaxOutputTokens : 384000;

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false,
            ["max_tokens"] = maxTokens,
            ["thinking"] = new Dictionary<string, string> { ["type"] = "enabled" }
        };

        if (request.Temperature.HasValue) payload["temperature"] = request.Temperature.Value;
        if (request.TopP.HasValue) payload["top_p"] = request.TopP.Value;

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ChatCompletionResponse { Content = $"deepseek API error ({(int)response.StatusCode}): {responseText}", FinishReason = "error", LatencyMs = 0 };

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        int inputTokens = 0, outputTokens = 0, cacheHitTokens = 0;
        string content = string.Empty;
        string? finishReason = null;
        string? reasoningContent = null;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("content", out var cnt))
                    content = cnt.GetString() ?? string.Empty;
                if (msg.TryGetProperty("reasoning_content", out var rc))
                    reasoningContent = rc.GetString();
            }
            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                finishReason = fr.GetString();
        }

        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct)) outputTokens = ct.GetInt32();
            if (usage.TryGetProperty("prompt_tokens_details", out var details) &&
                details.TryGetProperty("cached_tokens", out var cached)) cacheHitTokens = cached.GetInt32();
        }

        sw.Stop();

        if (!string.IsNullOrWhiteSpace(reasoningContent))
        {
            System.Diagnostics.Debug.WriteLine($"[DeepSeek Reasoning] {reasoningContent[..Math.Min(reasoningContent.Length, 500)]}...");
        }

        return new ChatCompletionResponse
        {
            Content = content,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            FinishReason = finishReason ?? "stop",
            Usage = new TokenUsage { InputTokens = inputTokens, OutputTokens = outputTokens, CacheHitTokens = cacheHitTokens }
        };
    }

    private string GetEndpoint()
    {
        var baseUrl = (_configuration["DEEPSEEK_BASE_URL"] ?? "https://api.deepseek.com").TrimEnd('/');
        return $"{baseUrl}/chat/completions";
    }
}
