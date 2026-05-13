using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// Azure OpenAI 聊天 Provider。
/// </summary>
public sealed class AzureChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 Azure Provider。
    /// </summary>
    public AzureChatProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId => "azure";

    /// <summary>
    /// 调用 Azure OpenAI 聊天接口。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"];
        var apiVersion = _configuration["AZURE_OPENAI_VERSION"];
        var apiKey = _configuration["AZURE_OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiVersion) || string.IsNullOrWhiteSpace(apiKey))
        {
            return "AZURE_OPENAI_API_KEY、AZURE_OPENAI_ENDPOINT 或 AZURE_OPENAI_VERSION 未配置。";
        }

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(request.Model)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";
        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Add("api-key", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new Dictionary<string, object?>
        {
            ["messages"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["role"] = "user",
                    ["content"] = request.Prompt
                }
            },
            ["stream"] = false,
            ["temperature"] = request.Temperature ?? 0.7
        };

        if (request.TopP.HasValue)
        {
            payload["top_p"] = request.TopP.Value;
        }

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return $"Azure API error ({(int)response.StatusCode}): {responseText}";
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        return responseText;
    }
}
