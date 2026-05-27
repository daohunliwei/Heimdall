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

        var effectiveModel = options?.ModelId ?? _model;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{effectiveModel}:generateContent?key={_apiKey}";
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var candidate = root.GetProperty("candidates")[0];
        var candidateContent = candidate.GetProperty("content");
        var parts = candidateContent.GetProperty("parts");

        // 构建响应消息，支持 Function Call 和文本内容
        var contents = new List<AIContent>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out var funcCall))
            {
                var name = funcCall.GetProperty("name").GetString() ?? string.Empty;
                var argsJson = funcCall.TryGetProperty("args", out var argsProp)
                    ? argsProp.GetRawText()
                    : "{}";

                contents.Add(new FunctionCallContent(name, argsJson));
            }
            else if (part.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                    contents.Add(new TextContent(text));
            }
        }

        // 如果没有 AIContent，回退到纯文本
        if (contents.Count == 0)
        {
            contents.Add(new TextContent(parts[0].TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""));
        }

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

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
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

        var effectiveModel = options?.ModelId ?? _model;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{effectiveModel}:streamGenerateContent?alt=sse&key={_apiKey}";
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

        var contents = new List<object>();

        foreach (var m in conversationMessages)
        {
            var role = m.Role == ChatRole.User ? "user" : "model";
            var parts = new List<object>();

            // 检查是否包含 FunctionCallContent（工具调用请求）
            foreach (var content in m.Contents)
            {
                if (content is FunctionCallContent fcc)
                {
                    parts.Add(new
                    {
                        functionCall = new
                        {
                            name = fcc.Name,
                            args = fcc.Arguments ?? new Dictionary<string, object?>()
                        }
                    });
                }
                else if (content is FunctionResultContent frc)
                {
                    parts.Add(new
                    {
                        functionResponse = new
                        {
                            name = frc.CallId ?? "unknown",
                            response = new { result = frc.Result ?? string.Empty }
                        }
                    });
                }
            }

            // 如果没有 Function 内容，使用文本
            if (parts.Count == 0)
            {
                parts.Add(new { text = m.Text ?? string.Empty });
            }

            contents.Add(new { role, parts });
        }

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

            // Gemini Function Calling 支持
            if (options.Tools is { Count: > 0 })
            {
                var functionDeclarations = new List<object>();
                foreach (var tool in options.Tools)
                {
                    if (tool is AIFunction func)
                    {
                        functionDeclarations.Add(new
                        {
                            name = func.Name,
                            description = func.Description ?? string.Empty,
                        });
                    }
                }

                if (functionDeclarations.Count > 0)
                {
                    requestObj["tools"] = new[]
                    {
                        new { functionDeclarations }
                    };
                }
            }
        }

        return requestObj;
    }

    public ChatClientMetadata Metadata => new("Google Gemini", new Uri("https://generativelanguage.googleapis.com"), _model);

    public TService? GetService<TService>(object? key = null) where TService : class
        => this as TService;

    object? IChatClient.GetService(Type serviceType, object? serviceKey)
        => serviceKey is not null ? null
            : serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this : null;

    void IDisposable.Dispose() { }
}
