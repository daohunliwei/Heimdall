using System.Text.Json;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Tasks;
using Heimdall.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ChatMessageBuilderService _chatMessageBuilder;
    private readonly IPromptMergeService _promptMergeService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IServiceProvider serviceProvider,
        ChatMessageBuilderService chatMessageBuilder,
        IPromptMergeService promptMergeService,
        ILogger<ChatController> logger)
    {
        _serviceProvider = serviceProvider;
        _chatMessageBuilder = chatMessageBuilder;
        _promptMergeService = promptMergeService;
        _logger = logger;
    }

    /// <summary>
    /// POST /chat/completions/stream — 真 SSE 流式聊天补全（基于 IChatClient.GetStreamingResponseAsync）
    /// </summary>
    [HttpPost("completions/stream")]
    public async Task StreamChat(CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        ChatCompletionRequest request;
        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            request = JsonSerializer.Deserialize<ChatCompletionRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ChatCompletionRequest();
        }
        catch
        {
            request = new ChatCompletionRequest();
        }

        var providerId = request.Provider ?? "ollama";
        var model = request.Model ?? request.CustomModel ?? string.Empty;

        try
        {
            var chatClient = _serviceProvider.GetRequiredKeyedService<IChatClient>(providerId);
            var latestUserPrompt = request.Messages
                .LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                ?.Content;
            var prompt = !string.IsNullOrWhiteSpace(latestUserPrompt)
                ? latestUserPrompt
                : request.Messages.LastOrDefault()?.Content ?? "Hello";

            var (systemPrompt, userPrompt) = await _promptMergeService.BuildChatPromptAsync(
                "chat", providerId, "text",
                new Dictionary<string, string> { ["question"] = prompt });

            var finalPrompt = string.IsNullOrWhiteSpace(userPrompt) ? prompt : userPrompt;
            var messages = _chatMessageBuilder.BuildChatMessages(request.Messages, systemPrompt, finalPrompt);

            var options = new ChatOptions
            {
                MaxOutputTokens = 4096,
            };
            if (!string.IsNullOrWhiteSpace(model))
            {
                options.ModelId = model;
            }

            // 真流式：通过 await foreach 消费 ChatResponseUpdate
            await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, ct))
            {
                if (ct.IsCancellationRequested) break;

                if (!string.IsNullOrEmpty(update.Text))
                {
                    var sseData = $"data: {JsonSerializer.Serialize(new { content = update.Text })}\n\n";
                    await Response.WriteAsync(sseData, ct);
                    await Response.Body.FlushAsync(ct);
                }

                if (update.FinishReason.HasValue)
                {
                    break;
                }
            }

            await Response.WriteAsync("event: done\ndata: [DONE]\n\n", ct);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式聊天失败");
            try
            {
                await Response.WriteAsync($"event: error\ndata: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n", ct);
            }
            catch { /* 客户端已断开 */ }
        }
    }
}
