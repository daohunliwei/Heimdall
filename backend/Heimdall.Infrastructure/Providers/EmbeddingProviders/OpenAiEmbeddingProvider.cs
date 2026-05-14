using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.EmbeddingProviders;

/// <summary>
/// OpenAI 嵌入向量 Provider。
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 OpenAI 嵌入 Provider。
    /// </summary>
    public OpenAiEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    public string EmbedderType => "openai";

    /// <summary>
    /// 生成单条文本向量。
    /// </summary>
    public async Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken)
    {
        var result = await EmbedBatchAsync(new[] { input }, cancellationToken);
        return result.FirstOrDefault() ?? Array.Empty<float>();
    }

    /// <summary>
    /// 批量生成文本向量。
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY 未配置。");
        }

        var endpoint = $"{(_configuration["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1").TrimEnd('/')}/embeddings";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new
        {
            model = "text-embedding-3-small",
            input = inputs,
            dimensions = 256,
            encoding_format = "float"
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        var data = document.RootElement.GetProperty("data");
        return data.EnumerateArray()
            .OrderBy(item => item.GetProperty("index").GetInt32())
            .Select(item => item.GetProperty("embedding").EnumerateArray().Select(number => number.GetSingle()).ToArray())
            .ToList();
    }
}
