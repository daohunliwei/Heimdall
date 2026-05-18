using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// Google Gemini 聊天 Provider。
/// </summary>
public sealed class GoogleChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 Google 聊天 Provider。
    /// </summary>
    public GoogleChatProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId => "google";

    /// <summary>
    /// 调用 Gemini 生成文本。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GOOGLE_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "GOOGLE_API_KEY not configured. Please set this environment variable to use Google models.";
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var promptText = !string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? $"{request.SystemPrompt}\n\n{request.Prompt}"
            : request.Prompt;
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = promptText }
                    }
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature ?? 1.0,
                topP = request.TopP ?? 0.8,
                topK = request.TopK ?? 20
            }
        };

        using var response = await _httpClient.PostAsync(
            endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return $"Google API error ({(int)response.StatusCode}): {responseText}";
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
        {
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            var builder = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    builder.Append(textElement.GetString());
                }
            }

            return builder.ToString();
        }

        return responseText;
    }
}
