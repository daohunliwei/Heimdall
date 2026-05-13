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
    /// 发起聊天补全请求。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["MINIMAX_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "MINIMAX_API_KEY not configured. Please set this environment variable to use MiniMax.";
        }

        var endpoint = GetEndpoint();
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["role"] = "user",
                    ["content"] = request.Prompt
                }
            },
            ["stream"] = false
        };

        if (request.Temperature.HasValue)
        {
            payload["temperature"] = Clamp01Exclusive(request.Temperature.Value);
        }

        if (request.TopP.HasValue)
        {
            payload["top_p"] = Clamp01Exclusive(request.TopP.Value);
        }

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"minimax API error ({(int)response.StatusCode}): {responseText}";
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() ?? string.Empty;
            }
        }

        return responseText;
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
