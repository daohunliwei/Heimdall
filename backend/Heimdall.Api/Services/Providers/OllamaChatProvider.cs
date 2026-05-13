using System.Text;
using System.Text.Json;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;

namespace Heimdall.Api.Services.Providers;

/// <summary>
/// Ollama 聊天 Provider。
/// </summary>
public sealed class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<OllamaChatProvider> _logger;

    /// <summary>
    /// 初始化 Ollama 聊天 Provider。
    /// </summary>
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

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId => "ollama";

    /// <summary>
    /// 调用 Ollama 生成文本。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = (_configuration["OLLAMA_HOST"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var endpoint = $"{baseUrl}/api/generate";
        var timeout = _configService.GetOllamaRequestTimeout();
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["prompt"] = request.Prompt,
            ["stream"] = false
        };

        if (request.Options is not null)
        {
            payload["options"] = request.Options.ToDictionary(item => item.Key, item => (object?)ConvertJsonElement(item.Value));
        }

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
            return document.RootElement.TryGetProperty("response", out var responseElement)
                ? (responseElement.GetString() ?? string.Empty).Replace("<think>", string.Empty, StringComparison.Ordinal).Replace("</think>", string.Empty, StringComparison.Ordinal)
                : responseText;
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

    /// <summary>
    /// 将 JsonElement 转换为普通对象。
    /// </summary>
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
