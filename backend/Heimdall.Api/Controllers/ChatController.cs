using System.Text.Json;
using Heimdall.Core.Services.Tasks;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly TextUtilityService _textUtility;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ProviderRegistry providerRegistry,
        TextUtilityService textUtility,
        ILogger<ChatController> logger)
    {
        _providerRegistry = providerRegistry;
        _textUtility = textUtility;
        _logger = logger;
    }

    /// <summary>
    /// POST /chat/completions/stream — SSE 流式聊天补全。
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
            request = JsonSerializer.Deserialize<ChatCompletionRequest>(body)
                ?? new ChatCompletionRequest();
        }
        catch
        {
            request = new ChatCompletionRequest();
        }

        try
        {
            var (_, _, _, provider) = _providerRegistry.ResolveChatProvider(request);
            var prompt = request.Messages.Count > 0
                ? request.Messages[^1].Content
                : "Hello";

            var systemPrompt = "You are a helpful code analysis assistant. Respond in the same language as the user's query.";
            var fullPrompt = $"{systemPrompt}\n\nUser query: {prompt}";

            var providerRequest = new ProviderChatRequest
            {
                ProviderId = provider.ProviderId,
                Model = request.Model ?? string.Empty,
                Prompt = fullPrompt
            };

            var result = await provider.GenerateAsync(providerRequest, ct);

            // 按 SSE 格式输出
            var chunks = _textUtility.SplitIntoSseChunks(result, 200);
            foreach (var chunk in chunks)
            {
                if (ct.IsCancellationRequested) break;
                var sseData = $"data: {JsonSerializer.Serialize(new { content = chunk })}\n\n";
                await Response.WriteAsync(sseData, ct);
                await Response.Body.FlushAsync(ct);
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
            await Response.WriteAsync($"event: error\ndata: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n", ct);
        }
    }
}
