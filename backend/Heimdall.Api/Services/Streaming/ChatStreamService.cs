using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Streaming;

/// <summary>
/// 负责把聊天结果按 SSE 协议写入响应流。
/// </summary>
public sealed class ChatStreamService
{
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化流式输出服务。
    /// </summary>
    public ChatStreamService(TextUtilityService textUtilityService)
    {
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 将文本内容写为 SSE 响应。
    /// </summary>
    public async Task WriteAsync(HttpResponse response, string content, CancellationToken cancellationToken)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache, no-transform";

        foreach (var chunk in _textUtilityService.SplitIntoSseChunks(content, 160))
        {
            foreach (var line in chunk.Split('\n'))
            {
                await response.WriteAsync($"data: {line}\n", cancellationToken);
            }

            await response.WriteAsync("\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
            await Task.Delay(10, cancellationToken);
        }

        await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
