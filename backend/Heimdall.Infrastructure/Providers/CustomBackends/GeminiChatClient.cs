using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers.CustomBackends;

/// <summary>
/// Google Gemini IChatClient 适配器
/// </summary>
public class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiChatClient> _logger;

    public GeminiChatClient(HttpClient httpClient, string apiKey, string model, ILogger<GeminiChatClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequest(messages, options);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var text = root.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? string.Empty;

        var usage = new UsageDetails();
        if (root.TryGetProperty("usageMetadata", out var usageMeta))
        {
            if (usageMeta.TryGetProperty("promptTokenCount", out var ptc))
                usage.InputTokenCount = ptc.GetInt32();
            if (usageMeta.TryGetProperty("candidatesTokenCount", out var ctc))
                usage.OutputTokenCount = ctc.GetInt32();
            if (usageMeta.TryGetProperty("totalTokenCount", out var ttc))
                usage.TotalTokenCount = ttc.GetInt32();
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
        var requestBody = BuildRequest(messages, options);
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:streamGenerateContent?alt=sse&key={_apiKey}";
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

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

            var delta = ParseGeminiData(data);
            if (delta is not null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            }
        }

        yield return new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop };
    }

    private static string? ParseGeminiData(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var msgContent) &&
                    msgContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var delta = parts[0].TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                    return string.IsNullOrEmpty(delta) ? null : delta;
                }
            }
            return null;
        }
        catch (JsonException) { return null; }
    }

    private object BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var msgList = messages.ToList();
        var systemMessages = msgList.Where(m => m.Role == ChatRole.System).ToList();
        var conversationMessages = msgList.Where(m => m.Role != ChatRole.System).ToList();

        var contents = conversationMessages.Select(m => new
        {
            role = m.Role == ChatRole.User ? "user" : "model",
            parts = new[] { new { text = m.Text ?? string.Empty } }
        }).ToList();

        var requestObj = new Dictionary<string, object>
        {
            ["contents"] = contents,
        };

        if (systemMessages.Count > 0)
        {
            requestObj["systemInstruction"] = new
            {
                parts = new[] { new { text = string.Join("\n", systemMessages.Select(m => m.Text)) } }
            };
        }

        if (options != null)
        {
            var generationConfig = new Dictionary<string, object>();
            if (options.Temperature.HasValue) generationConfig["temperature"] = options.Temperature.Value;
            if (options.MaxOutputTokens.HasValue) generationConfig["maxOutputTokens"] = options.MaxOutputTokens.Value;
            if (options.TopP.HasValue) generationConfig["topP"] = options.TopP.Value;
            if (generationConfig.Count > 0) requestObj["generationConfig"] = generationConfig;
        }

        return requestObj;
    }

    void IDisposable.Dispose() { }
    object? IChatClient.GetService(Type serviceType, object? serviceKey) => null;
}
