using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Logging;

public sealed class StructuredLogger : IStructuredLogger
{
    private readonly ILogger<StructuredLogger> _logger;

    public StructuredLogger(ILogger<StructuredLogger> logger)
    {
        _logger = logger;
    }

    public void LogTaskProgress(Guid taskId, string step, int? currentPage, int? totalPages, string message)
    {
        var pageInfo = currentPage.HasValue && totalPages.HasValue
            ? $" {currentPage}/{totalPages} 页 |"
            : string.Empty;
        _logger.LogInformation("[WikiTask]{PageInfo} 步骤: {Step} | {Message} | TaskId: {TaskId}",
            pageInfo, step, message, taskId.ToString("N")[..8]);
    }

    public void LogLlmCall(Guid taskId, string provider, string model, int promptLength, long latencyMs)
    {
        _logger.LogInformation("[LLM] Provider={Provider} Model={Model} PromptLen={PromptLen} LatencyMs={LatencyMs} | TaskId: {TaskId}",
            provider, model, promptLength, latencyMs, taskId.ToString("N")[..8]);
    }

    public void LogTaskSummary(Guid taskId, int totalPages, double totalElapsedSeconds, int llmCallCount, int inputTokens, int outputTokens)
    {
        _logger.LogInformation(
            "[WikiTask] 生成完成 | 总页数: {Pages} | 总耗时: {Elapsed:F1}s | LLM 调用: {Calls} 次 | Token: 输入 {InputK}K / 输出 {OutputK}K | TaskId: {TaskId}",
            totalPages, totalElapsedSeconds, llmCallCount, inputTokens / 1000, outputTokens / 1000, taskId.ToString("N")[..8]);
    }
}
