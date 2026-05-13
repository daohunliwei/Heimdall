using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;

using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.EmbeddingProviders;

/// <summary>
/// Bedrock 嵌入向量 Provider。
/// </summary>
public sealed class BedrockEmbeddingProvider : IEmbeddingProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 Bedrock 嵌入 Provider。
    /// </summary>
    public BedrockEmbeddingProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    public string EmbedderType => "bedrock";

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
        var region = RegionEndpoint.GetBySystemName(_configuration["AWS_REGION"] ?? "us-east-1");
        var accessKey = _configuration["AWS_ACCESS_KEY_ID"];
        var secretKey = _configuration["AWS_SECRET_ACCESS_KEY"];
        AWSCredentials credentials = !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
            ? new BasicAWSCredentials(accessKey, secretKey)
            : FallbackCredentialsFactory.GetCredentials();

        using var client = new AmazonBedrockRuntimeClient(credentials, region);
        var result = new List<float[]>();
        foreach (var input in inputs)
        {
            var payload = JsonSerializer.Serialize(new
            {
                inputText = input,
                dimensions = 256
            });

            var response = await client.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = "amazon.titan-embed-text-v2:0",
                Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload))
            }, cancellationToken);

            using var reader = new StreamReader(response.Body);
            var responseText = await reader.ReadToEndAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseText);
            result.Add(document.RootElement.GetProperty("embedding").EnumerateArray().Select(number => number.GetSingle()).ToArray());
        }

        return result;
    }
}
