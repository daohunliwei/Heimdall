using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Providers.ChatProviders;

/// <summary>
/// AWS Bedrock 聊天 Provider。
/// </summary>
public sealed class BedrockChatProvider : IChatProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化 Bedrock Provider。
    /// </summary>
    public BedrockChatProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId => "bedrock";

    /// <summary>
    /// 调用 Bedrock 模型。
    /// </summary>
    public async Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var client = await CreateClientAsync(cancellationToken);
            var provider = GetModelProvider(request.Model);
            var body = JsonSerializer.Serialize(BuildRequestBody(provider, request));
            var response = await client.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = request.Model,
                Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body))
            }, cancellationToken);

            using var reader = new StreamReader(response.Body);
            var responseText = await reader.ReadToEndAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseText);
            return ExtractResponseText(provider, document.RootElement);
        }
        catch (Exception exception)
        {
            return $"Error with AWS Bedrock API: {exception.Message}";
        }
    }

    /// <summary>
    /// 创建 Bedrock 客户端。
    /// </summary>
    private async Task<AmazonBedrockRuntimeClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var region = _configuration["AWS_REGION"] ?? "us-east-1";
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);
        var accessKey = _configuration["AWS_ACCESS_KEY_ID"];
        var secretKey = _configuration["AWS_SECRET_ACCESS_KEY"];
        var sessionToken = _configuration["AWS_SESSION_TOKEN"];
        var roleArn = _configuration["AWS_ROLE_ARN"];

        AWSCredentials credentials;
        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            credentials = string.IsNullOrWhiteSpace(sessionToken)
                ? new BasicAWSCredentials(accessKey, secretKey)
                : new SessionAWSCredentials(accessKey, secretKey, sessionToken);
        }
        else
        {
            credentials = FallbackCredentialsFactory.GetCredentials();
        }

        if (!string.IsNullOrWhiteSpace(roleArn))
        {
            using var stsClient = new AmazonSecurityTokenServiceClient(credentials, regionEndpoint);
            var assumeRoleResponse = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "HeimdallBedrockSession"
            }, cancellationToken);

            credentials = new SessionAWSCredentials(
                assumeRoleResponse.Credentials.AccessKeyId,
                assumeRoleResponse.Credentials.SecretAccessKey,
                assumeRoleResponse.Credentials.SessionToken);
        }

        return new AmazonBedrockRuntimeClient(credentials, regionEndpoint);
    }

    /// <summary>
    /// 判断模型归属的 Provider。
    /// </summary>
    private static string GetModelProvider(string modelId)
    {
        var segments = modelId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 ? segments[1] : segments[0];
    }

    /// <summary>
    /// 根据 Provider 构建请求体。
    /// </summary>
    private static object BuildRequestBody(string provider, ProviderChatRequest request)
    {
        var hasSystem = !string.IsNullOrWhiteSpace(request.SystemPrompt);

        return provider switch
        {
            "anthropic" => new
            {
                anthropic_version = "bedrock-2023-05-31",
                system = hasSystem ? request.SystemPrompt : null,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "text", text = request.Prompt }
                        }
                    }
                },
                max_tokens = 4096,
                temperature = request.Temperature ?? 0.7,
                top_p = request.TopP ?? 0.8
            },
            "amazon" => new
            {
                inputText = hasSystem ? $"{request.SystemPrompt}\n\n{request.Prompt}" : request.Prompt,
                textGenerationConfig = new
                {
                    maxTokenCount = 4096,
                    temperature = request.Temperature ?? 0.7,
                    topP = request.TopP ?? 0.8,
                    stopSequences = Array.Empty<string>()
                }
            },
            "cohere" => new
            {
                prompt = hasSystem ? $"{request.SystemPrompt}\n\n{request.Prompt}" : request.Prompt,
                max_tokens = 4096,
                temperature = request.Temperature ?? 0.7,
                p = request.TopP ?? 0.8
            },
            "ai21" => new
            {
                prompt = hasSystem ? $"{request.SystemPrompt}\n\n{request.Prompt}" : request.Prompt,
                maxTokens = 4096,
                temperature = request.Temperature ?? 0.7,
                topP = request.TopP ?? 0.8
            },
            _ => new { prompt = hasSystem ? $"{request.SystemPrompt}\n\n{request.Prompt}" : request.Prompt }
        };
    }

    /// <summary>
    /// 从 Bedrock 响应体中提取正文。
    /// </summary>
    private static string ExtractResponseText(string provider, JsonElement response)
    {
        return provider switch
        {
            "anthropic" => response.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty,
            "amazon" => response.GetProperty("results")[0].GetProperty("outputText").GetString() ?? string.Empty,
            "cohere" => response.GetProperty("generations")[0].GetProperty("text").GetString() ?? string.Empty,
            "ai21" => response.GetProperty("completions")[0].GetProperty("data").GetProperty("text").GetString() ?? string.Empty,
            _ => response.ToString()
        };
    }
}
