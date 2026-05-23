using System.Text.Json;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Tasks;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly ChatClientFactory _chatClientFactory;
    private readonly TextUtilityService _textUtility;
    private readonly IPromptMergeService _promptMergeService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ChatClientFactory chatClientFactory,
        TextUtilityService textUtility,
        IPromptMergeService promptMergeService,
        ILogger<ChatController> logger)
    {
        _chatClientFactory = chatClientFactory;
        _textUtility = textUtility;
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
            var chatClient = _chatClientFactory.GetClient(providerId);
            var prompt = request.Messages.Count > 0
                ? request.Messages[^1].Content
                : "Hello";

            var (systemPrompt, userPrompt) = await _promptMergeService.BuildChatPromptAsync(
                "chat", providerId, "text",
                new Dictionary<string, string> { ["question"] = prompt });

            var finalPrompt = string.IsNullOrEmpty(userPrompt) ? prompt : userPrompt;

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.System, systemPrompt));
            }
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, finalPrompt));

            var options = new ChatOptions
            {
                ModelId = model,
                MaxOutputTokens = 4096,
            };

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
