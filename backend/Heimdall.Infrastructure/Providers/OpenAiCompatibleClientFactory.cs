using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace Heimdall.Infrastructure.Providers;

/// <summary>
/// OpenAI 兼容 Client 工厂 — 统一管理 5 个 OpenAI 兼容 Provider 的 IChatClient 创建
/// </summary>
public static class OpenAiCompatibleClientFactory
{
    public static IChatClient Create(IConfiguration configuration, string providerId, string model)
    {
        var apiKey = configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_API_KEY"]
            ?? Environment.GetEnvironmentVariable($"{providerId.ToUpperInvariant()}_API_KEY")
            ?? string.Empty;

        var endpoint = configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_ENDPOINT"];

        return providerId.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAi(apiKey, model),
            "openrouter" => CreateOpenRouter(apiKey, model),
            "dashscope" => CreateDashScope(apiKey, model),
            "deepseek" => CreateDeepSeek(apiKey, model),
            _ => throw new ArgumentException($"不支持的 OpenAI 兼容 Provider: {providerId}")
        };
    }

    private static IChatClient CreateOpenAi(string apiKey, string model)
    {
        var openAiClient = new OpenAIClient(apiKey);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateOpenRouter(string apiKey, string model)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1")
        };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateDashScope(string apiKey, string model)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1")
        };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateDeepSeek(string apiKey, string model)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.deepseek.com/v1")
        };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// 创建 Azure OpenAI 的 IChatClient（OpenAI 兼容模式）
    /// </summary>
    public static IChatClient CreateAzure(IConfiguration configuration, string providerId, string model)
    {
        var apiKey = configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_API_KEY"]
            ?? string.Empty;
        var endpoint = configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_ENDPOINT"]
            ?? "https://api.openai.com/v1";

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return openAiClient.GetChatClient(model).AsIChatClient();
    }
}
