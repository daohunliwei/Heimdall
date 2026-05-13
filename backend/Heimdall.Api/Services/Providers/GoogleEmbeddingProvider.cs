using System.Text;
using System.Text.Json;

namespace Heimdall.Api.Services.Providers;

/// <summary>
/// Google Gemini 嵌入向量 Provider。
/// </summary>
public sealed class GoogleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 Google 嵌入 Provider。
    /// </summary>
    public GoogleEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    public string EmbedderType => "google";

    /// <summary>
    /// 生成单条文本向量。
    /// </summary>
    public async Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken)
    {
        var results = await EmbedBatchAsync(new[] { input }, cancellationToken);
        return results.FirstOrDefault() ?? Array.Empty<float>();
    }

    /// <summary>
    /// 批量生成文本向量。
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GOOGLE_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("GOOGLE_API_KEY 未配置。");
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents?key={Uri.EscapeDataString(apiKey)}";
        var payload = new
        {
            requests = inputs.Select(text => new
            {
                model = "models/gemini-embedding-001",
                content = new
                {
                    parts = new[]
                    {
                        new { text }
                    }
                },
                taskType = "SEMANTIC_SIMILARITY"
            }).ToArray()
        };

        using var response = await _httpClient.PostAsync(
            endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        var embeddings = document.RootElement.GetProperty("embeddings");
        return embeddings.EnumerateArray()
            .Select(item => item.GetProperty("values").EnumerateArray().Select(number => number.GetSingle()).ToArray())
            .ToList();
    }
}
