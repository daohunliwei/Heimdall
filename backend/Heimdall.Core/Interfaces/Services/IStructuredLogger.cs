namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// 结构化日志接口 — 为任务执行提供带上下文的结构化进度日志。
/// </summary>
public interface IStructuredLogger
{
    /// <summary>记录任务进度（当前步骤、页码进度等）</summary>
    void LogTaskProgress(Guid taskId, string step, int? currentPage, int? totalPages, string message);

    /// <summary>记录 LLM 调用摘要</summary>
    void LogLlmCall(Guid taskId, string provider, string model, int promptLength, long latencyMs);

    /// <summary>记录任务完成汇总</summary>
    void LogTaskSummary(Guid taskId, int totalPages, double totalElapsedSeconds, int llmCallCount, int inputTokens, int outputTokens);
}
