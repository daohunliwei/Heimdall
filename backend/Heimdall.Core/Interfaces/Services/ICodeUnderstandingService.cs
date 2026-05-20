using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// 深度代码理解服务接口——编排调用图/依赖拓扑/设计模式检测/LLM架构理解。
/// </summary>
public interface ICodeUnderstandingService
{
    /// <summary>
    /// 对指定仓库版本执行深度代码理解分析。
    /// </summary>
    Task<CodeUnderstandingResult> AnalyzeAsync(
        Guid repositoryVersionId,
        string repoPath,
        string provider,
        string? model,
        CancellationToken ct = default);
}
