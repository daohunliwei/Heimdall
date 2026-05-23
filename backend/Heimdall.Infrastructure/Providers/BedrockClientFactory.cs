using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers;

/// <summary>
/// Bedrock IChatClient 工厂 — 使用 AWS Bedrock Converse API
/// </summary>
public static class BedrockClientFactory
{
    public static IChatClient Create(IConfiguration configuration, string model, ILoggerFactory loggerFactory)
    {
        var region = configuration["HEIMDALL_BEDROCK_REGION"] ?? "us-east-1";
        var accessKey = configuration["HEIMDALL_BEDROCK_ACCESS_KEY"];
        var secretKey = configuration["HEIMDALL_BEDROCK_SECRET_KEY"];

        AWSCredentials credentials;
        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            credentials = new BasicAWSCredentials(accessKey, secretKey);
        }
        else
        {
            credentials = FallbackCredentialsFactory.GetCredentials();
        }

        var bedrockClient = new AmazonBedrockRuntimeClient(credentials, RegionEndpoint.GetBySystemName(region));
        return new BedrockConverseChatClient(bedrockClient, model, loggerFactory.CreateLogger<BedrockConverseChatClient>());
    }
}

/// <summary>
/// Bedrock Converse API 的 IChatClient 适配器
/// </summary>
public class BedrockConverseChatClient : IChatClient
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly string _model;
    private readonly ILogger<BedrockConverseChatClient> _logger;

    public BedrockConverseChatClient(AmazonBedrockRuntimeClient client, string model, ILogger<BedrockConverseChatClient> logger)
    {
        _client = client;
        _model = model;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = BuildConverseRequest(messages, options);
        var response = await _client.ConverseAsync(request, cancellationToken);

        var text = response.Output?.Message?.Content?
            .Select(c => c.Text)
            .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? string.Empty;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails
            {
                InputTokenCount = response.Usage?.InputTokens ?? 0,
                OutputTokenCount = response.Usage?.OutputTokens ?? 0,
                TotalTokenCount = response.Usage?.TotalTokens ?? 0,
            },
            ResponseId = Guid.NewGuid().ToString(),
            ModelId = _model,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildConverseStreamRequest(messages, options);
        var response = await _client.ConverseStreamAsync(request, cancellationToken);

        await foreach (var chunk in response.Stream.WithCancellation(cancellationToken))
        {
            if (chunk is ContentBlockDeltaEvent deltaEvent)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, deltaEvent.Delta?.Text ?? string.Empty);
            }
            else if (chunk is MessageStopEvent stopEvent)
            {
                yield return new ChatResponseUpdate
                {
                    FinishReason = stopEvent.StopReason == "end_turn" || stopEvent.StopReason == "stop_sequence"
                        ? ChatFinishReason.Stop
                        : ChatFinishReason.Length,
                };
            }
        }
    }

    private ConverseRequest BuildConverseRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var converseReq = new ConverseRequest
        {
            ModelId = _model,
            InferenceConfig = new InferenceConfiguration
            {
                Temperature = (float)(options?.Temperature ?? 0.7f),
                MaxTokens = options?.MaxOutputTokens ?? 8192,
                TopP = (float)(options?.TopP ?? 1.0f),
            }
        };

        var msgList = messages.ToList();

        // 提取 system 消息
        var systemTexts = msgList.Where(m => m.Role == ChatRole.System)
            .Select(m => m.Text).ToList();
        if (systemTexts.Count > 0)
        {
            converseReq.System = systemTexts.Select(t => new SystemContentBlock { Text = t }).ToList();
        }

        // 提取对话消息
        var conversationMessages = msgList.Where(m => m.Role != ChatRole.System).ToList();
        converseReq.Messages = conversationMessages.Select(m => new Amazon.BedrockRuntime.Model.Message
        {
            Role = m.Role == ChatRole.User ? ConversationRole.User : ConversationRole.Assistant,
            Content = new List<ContentBlock>
            {
                new() { Text = m.Text ?? string.Empty }
            }
        }).ToList();

        return converseReq;
    }

    private ConverseStreamRequest BuildConverseStreamRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var streamReq = new ConverseStreamRequest
        {
            ModelId = _model,
            InferenceConfig = new InferenceConfiguration
            {
                Temperature = (float)(options?.Temperature ?? 0.7f),
                MaxTokens = options?.MaxOutputTokens ?? 8192,
                TopP = (float)(options?.TopP ?? 1.0f),
            }
        };

        var msgList = messages.ToList();

        var systemTexts = msgList.Where(m => m.Role == ChatRole.System)
            .Select(m => m.Text).ToList();
        if (systemTexts.Count > 0)
        {
            streamReq.System = systemTexts.Select(t => new SystemContentBlock { Text = t }).ToList();
        }

        var conversationMessages = msgList.Where(m => m.Role != ChatRole.System).ToList();
        streamReq.Messages = conversationMessages.Select(m => new Amazon.BedrockRuntime.Model.Message
        {
            Role = m.Role == ChatRole.User ? ConversationRole.User : ConversationRole.Assistant,
            Content = new List<ContentBlock>
            {
                new() { Text = m.Text ?? string.Empty }
            }
        }).ToList();

        return streamReq;
    }

    void IDisposable.Dispose() { }
    object? IChatClient.GetService(Type serviceType, object? serviceKey) => null;
}
