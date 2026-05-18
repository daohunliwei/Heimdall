using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// Ollama 聊天 Provider，使用 /api/chat 端点以获得更好的指令遵循质量。
/// </summary>
public sealed class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<OllamaChatProvider> _logger;

    public OllamaChatProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        HeimdallConfigService configService,
        ILogger<OllamaChatProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _configService = configService;
        _logger = logger;
    }

    public string ProviderId => "ollama";

    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = (_configuration["HEIMDALL_OLLAMA_CHAT_HOST"] ?? _configuration["OLLAMA_HOST"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var endpoint = $"{baseUrl}/api/chat";
        var timeout = _configService.GetOllamaRequestTimeout();

        var options = new Dictionary<string, object?>();
        if (request.Options is not null)
        {
            foreach (var item in request.Options)
                options[item.Key] = ConvertJsonElement(item.Value);
        }

        // 从 ProviderChatRequest 或 Options 中提取参数映射到 Ollama API
        if (request.Temperature.HasValue && !options.ContainsKey("temperature"))
            options["temperature"] = request.Temperature.Value;
        if (request.TopP.HasValue && !options.ContainsKey("top_p"))
            options["top_p"] = request.TopP.Value;
        if (request.TopK.HasValue && !options.ContainsKey("top_k"))
            options["top_k"] = request.TopK.Value;

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }
        messages.Add(new { role = "user", content = request.Prompt });

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false
        };

        if (options.Count > 0)
            payload["options"] = options;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "开始调用 Ollama Chat Model={Model} Endpoint={Endpoint} TimeoutMinutes={TimeoutMinutes}",
            request.Model,
            endpoint,
            timeout.TotalMinutes);

        try
        {
            using var response = await _httpClient.PostAsync(
                endpoint,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                linkedCts.Token);
            var responseText = await response.Content.ReadAsStringAsync(linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama Chat 返回失败状态码 Model={Model} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                    request.Model,
                    (int)response.StatusCode,
                    (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
                return $"Ollama API error ({(int)response.StatusCode}): {responseText}";
            }

            using var document = JsonDocument.Parse(responseText);
            _logger.LogInformation(
                "Ollama Chat 调用完成 Model={Model} ElapsedMs={ElapsedMs} ResponseLength={ResponseLength}",
                request.Model,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                responseText.Length);

            var content = document.RootElement.TryGetProperty("message", out var messageElement)
                ? (messageElement.TryGetProperty("content", out var contentElement)
                    ? (contentElement.GetString() ?? string.Empty)
                    : string.Empty)
                : string.Empty;

            // 正确移除 think 标签及其内容
            content = Regex.Replace(content, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);

            return string.IsNullOrWhiteSpace(content) ? responseText : content;
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Ollama Chat 请求超时 Model={Model} TimeoutMinutes={TimeoutMinutes} ElapsedMs={ElapsedMs}",
                request.Model,
                timeout.TotalMinutes,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            throw new TimeoutException($"Ollama 请求超时，模型 `{request.Model}` 在 {timeout.TotalMinutes} 分钟内未完成");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(
                exception,
                "Ollama Chat 调用被取消 Model={Model} CallerCancellation={CallerCancellation} ElapsedMs={ElapsedMs}",
                request.Model,
                cancellationToken.IsCancellationRequested,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            throw;
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            _ => element.ToString()
        };
    }
}
