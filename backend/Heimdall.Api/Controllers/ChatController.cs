using Heimdall.Api.Models;
using Heimdall.Api.Services.Chat;
using Heimdall.Api.Services.Streaming;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供聊天流式输出接口。
/// </summary>
[ApiController]
[Route("chat/completions")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatOrchestratorService _chatOrchestratorService;
    private readonly ChatStreamService _chatStreamService;

    /// <summary>
    /// 初始化聊天控制器。
    /// </summary>
    public ChatController(ChatOrchestratorService chatOrchestratorService, ChatStreamService chatStreamService)
    {
        _chatOrchestratorService = chatOrchestratorService;
        _chatStreamService = chatStreamService;
    }

    /// <summary>
    /// 生成聊天结果并按 SSE 方式返回。
    /// </summary>
    [HttpPost("stream")]
    public async Task<IActionResult> StreamAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var generated = await _chatOrchestratorService.GenerateAsync(request, cancellationToken);
            await _chatStreamService.WriteAsync(Response, generated, cancellationToken);
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = exception.Message });
        }
    }
}
