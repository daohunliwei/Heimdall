using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// 基于 OpenAI Chat Completions 协议的 Provider 适配器，覆盖 OpenAI、OpenRouter 与 DashScope。
/// </summary>
public sealed class OpenAiCompatibleChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 OpenAI 兼容协议 Provider。
    /// </summary>
    public OpenAiCompatibleChatProvider(HttpClient httpClient, IConfiguration configuration, string providerId)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        ProviderId = providerId;
    }

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// 发起聊天补全请求。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var endpoint = GetEndpoint();
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GetMissingKeyMessage();
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (ProviderId == "openrouter")
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/AsyncFuncAI/repo-wiki-open");
            message.Headers.TryAddWithoutValidation("X-Title", "Heimdall");
        }

        if (ProviderId == "dashscope")
        {
            var workspaceId = _configuration["DASHSCOPE_WORKSPACE_ID"];
            if (!string.IsNullOrWhiteSpace(workspaceId))
            {
                message.Headers.TryAddWithoutValidation("X-DashScope-WorkSpace", workspaceId);
            }
        }

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
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.TopP.HasValue)
        {
            payload["top_p"] = request.TopP.Value;
        }

        if (ProviderId == "dashscope")
        {
            payload["extra_body"] = new Dictionary<string, object?>
            {
                ["enable_thinking"] = false
            };
        }

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"{ProviderId} API error ({(int)response.StatusCode}): {responseText}";
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
    /// 获取当前 provider 的终结点。
    /// </summary>
    private string GetEndpoint()
    {
        return ProviderId switch
        {
            "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
            "dashscope" => $"{(_configuration["DASHSCOPE_BASE_URL"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1").TrimEnd('/')}/chat/completions",
            _ => $"{(_configuration["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1").TrimEnd('/')}/chat/completions"
        };
    }

    /// <summary>
    /// 获取当前 provider 的 API Key。
    /// </summary>
    private string? GetApiKey()
    {
        return ProviderId switch
        {
            "openrouter" => _configuration["OPENROUTER_API_KEY"],
            "dashscope" => _configuration["DASHSCOPE_API_KEY"],
            _ => _configuration["OPENAI_API_KEY"]
        };
    }

    /// <summary>
    /// 获取缺失密钥时的友好错误信息。
    /// </summary>
    private string GetMissingKeyMessage()
    {
        return ProviderId switch
        {
            "openrouter" => "OPENROUTER_API_KEY not configured. Please set this environment variable to use OpenRouter.",
            "dashscope" => "DASHSCOPE_API_KEY not configured. Please set this environment variable to use DashScope.",
            _ => "OPENAI_API_KEY not configured. Please set this environment variable to use OpenAI."
        };
    }
}
