using System.Text;
using System.Text.Json;
using Heimdall.Infrastructure.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers.EmbeddingProviders;

/// <summary>
/// Ollama 嵌入向量 Provider。
/// </summary>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    /// <summary>
    /// 初始化 Ollama 嵌入 Provider。
    /// </summary>
    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        HeimdallConfigService configService,
        ILogger<OllamaEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    public string EmbedderType => "ollama";

    /// <summary>
    /// 生成单条文本向量。
    /// </summary>
    public async Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken)
    {
        var embedderConfig = _configService.GetActiveEmbedder();
        var baseUrl = (!string.IsNullOrWhiteSpace(embedderConfig.Host)
            ? embedderConfig.Host
            : _configuration["OLLAMA_HOST"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var endpoint = $"{baseUrl}/api/embeddings";
        var timeout = _configService.GetOllamaRequestTimeout();
        var model = embedderConfig.ModelKwargs.TryGetValue("model", out var modelElement)
            ? modelElement.GetString() ?? "nomic-embed-text"
            : "nomic-embed-text";
        var payload = new
        {
            model,
            prompt = input
        };

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var response = await _httpClient.PostAsync(
            endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            linkedCts.Token);
        var responseText = await response.Content.ReadAsStringAsync(linkedCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama Embedding 返回失败状态码 StatusCode={StatusCode} InputLength={InputLength}",
                (int)response.StatusCode,
                input.Length);
            throw new InvalidOperationException($"Ollama embedding API error ({(int)response.StatusCode}): {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);
        return document.RootElement.GetProperty("embedding").EnumerateArray().Select(number => number.GetSingle()).ToArray();
    }

    /// <summary>
    /// 批量生成文本向量。
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        var results = new List<float[]>();
        foreach (var input in inputs)
        {
            results.Add(await EmbedAsync(input, cancellationToken));
        }

        return results;
    }
}
