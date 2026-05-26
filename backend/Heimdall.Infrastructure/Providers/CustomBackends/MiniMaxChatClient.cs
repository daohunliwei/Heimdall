using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers.CustomBackends;

/// <summary>
/// MiniMax IChatClient 适配器
/// </summary>
public class MiniMaxChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<MiniMaxChatClient> _logger;
    private const string BaseUrl = "https://api.minimaxi.com/v1";

    public MiniMaxChatClient(HttpClient httpClient, string apiKey, string model, ILogger<MiniMaxChatClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options?.Tools is { Count: > 0 })
        {
            _logger.LogWarning("MiniMax 暂不支持 Tool Call，忽略 {ToolCount} 个工具", options.Tools.Count);
        }

        var requestBody = BuildRequest(messages, options, stream: false);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // 检查 API 错误
        if (root.TryGetProperty("base_resp", out var baseResp) &&
            baseResp.TryGetProperty("status_code", out var sc) && sc.GetInt32() != 0)
        {
            var msg = baseResp.TryGetProperty("status_msg", out var sm) ? sm.GetString() : "Unknown";
            throw new InvalidOperationException($"MiniMax API 错误 (code={sc.GetInt32()}): {msg}");
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            _logger.LogWarning("MiniMax 响应无 choices: {Response}", responseJson[..200]);
            throw new InvalidOperationException("MiniMax 响应不包含 choices");
        }

        var text = choices[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? string.Empty;

        var usage = new UsageDetails();
        if (root.TryGetProperty("usage", out var usageProp))
        {
            if (usageProp.TryGetProperty("prompt_tokens", out var pt))
                usage.InputTokenCount = pt.GetInt32();
            if (usageProp.TryGetProperty("completion_tokens", out var ct))
                usage.OutputTokenCount = ct.GetInt32();
            if (usageProp.TryGetProperty("total_tokens", out var tt))
                usage.TotalTokenCount = tt.GetInt32();
            // MiniMax 缓存 token
            if (usageProp.TryGetProperty("cache_read_input_tokens", out var cache))
                usage.AdditionalCounts["CachedInputTokenCount"] = cache.GetInt32();
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = usage,
            ResponseId = Guid.NewGuid().ToString(),
            ModelId = _model,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options?.Tools is { Count: > 0 })
        {
            _logger.LogWarning("MiniMax 暂不支持 Tool Call，忽略 {ToolCount} 个工具", options.Tools.Count);
        }

        var requestBody = BuildRequest(messages, options, stream: true);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("Accept", "text/event-stream");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            var deltaText = ParseMiniMaxData(data);
            if (deltaText is not null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, deltaText);
            }
        }

        yield return new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop };
    }

    private static string? ParseMiniMaxData(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var textProp))
                {
                    var result = textProp.GetString();
                    return string.IsNullOrEmpty(result) ? null : result;
                }
            }
            return null;
        }
        catch (JsonException) { return null; }
    }

    private object BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        var msgList = messages.Select(m => new
        {
            role = m.Role == ChatRole.System ? "system" : m.Role == ChatRole.User ? "user" : "assistant",
            content = m.Text ?? string.Empty
        }).ToList();

        return new
        {
            model = _model,
            messages = msgList,
            stream,
            temperature = options?.Temperature ?? 0.7f,
            max_tokens = options?.MaxOutputTokens ?? 8192,
            top_p = options?.TopP ?? 1.0f,
        };
    }

    public ChatClientMetadata Metadata => new("MiniMax", new Uri(BaseUrl), _model);

    public TService? GetService<TService>(object? key = null) where TService : class
        => this as TService;

    object? IChatClient.GetService(Type serviceType, object? serviceKey)
        => serviceKey is not null ? null
            : serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this : null;

    void IDisposable.Dispose() { }
}
