using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// Ask 派生任务服务接口。
/// 该接口用于在显式版本锚点上执行问答，并接入稳定的双向量检索结果。
/// </summary>
public interface IAskTaskService
{
    /// <summary>
    /// 执行一次 Ask 问答请求。
    /// </summary>
    Task<AskTaskExecutionResult> AskAsync(
        AskTaskExecutionRequest request,
        CancellationToken cancellationToken = default);
}
