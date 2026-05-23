using System.Runtime.CompilerServices;
using Heimdall.Core.Models;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// Ask 派生任务服务接口。
/// </summary>
public interface IAskTaskService
{
    /// <summary>
    /// 执行一次 Ask 问答请求（非流式）。
    /// </summary>
    Task<AskTaskExecutionResult> AskAsync(
        AskTaskExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行流式 Ask 问答，返回 IAsyncEnumerable 供 SSE 输出。
    /// </summary>
    IAsyncEnumerable<ChatResponseUpdate> AskStreamingAsync(
        AskTaskExecutionRequest request,
        CancellationToken cancellationToken = default);
}
