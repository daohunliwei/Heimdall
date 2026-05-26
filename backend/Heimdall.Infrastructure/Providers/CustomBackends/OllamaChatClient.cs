using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers.CustomBackends;

/// <summary>
/// Ollama IChatClient 适配器 — 基于 HttpClient 调用 Ollama API
/// </summary>
public class OllamaChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger<OllamaChatClient> _logger;

    public OllamaChatClient(HttpClient httpClient, string baseUrl, string model, ILogger<OllamaChatClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options?.Tools is { Count: > 0 })
        {
            _logger.LogWarning("Ollama 不支持 Tool Call，忽略 {ToolCount} 个工具", options.Tools.Count);
        }

        var requestBody = BuildRequest(messages, options, stream: false);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var text = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = new UsageDetails();
        if (root.TryGetProperty("eval_count", out var evalCount))
            usage.OutputTokenCount = evalCount.GetInt32();
        if (root.TryGetProperty("prompt_eval_count", out var promptCount))
            usage.InputTokenCount = promptCount.GetInt32();

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
            _logger.LogWarning("Ollama 不支持 Tool Call，忽略 {ToolCount} 个工具", options.Tools.Count);
        }

        var requestBody = BuildRequest(messages, options, stream: true);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            var result = ParseOllamaLine(line);
            if (result.text is not null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, result.text);
            }
            if (result.done)
            {
                yield return new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop };
            }
        }
    }

    private static (string? text, bool done) ParseOllamaLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            string? text = null;
            bool done = false;

            if (root.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentProp))
            {
                text = contentProp.GetString();
                if (string.IsNullOrEmpty(text)) text = null;
            }
            if (root.TryGetProperty("done", out var d) && d.GetBoolean())
            {
                done = true;
            }
            return (text, done);
        }
        catch (JsonException)
        {
            return (null, false);
        }
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
            options = options != null ? new
            {
                temperature = options.Temperature,
                num_predict = options.MaxOutputTokens,
            } : null
        };
    }

    public ChatClientMetadata Metadata => new("Ollama", new Uri(_baseUrl), _model);

    public TService? GetService<TService>(object? key = null) where TService : class
        => this as TService;

    object? IChatClient.GetService(Type serviceType, object? serviceKey)
        => serviceKey is not null ? null
            : serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this : null;

    void IDisposable.Dispose() { }
}
