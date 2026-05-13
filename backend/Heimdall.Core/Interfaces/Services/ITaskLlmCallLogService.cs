namespace Heimdall.Core.Interfaces.Services;

/// <summary>LLM call audit log for task execution tracing.</summary>
public interface ITaskLlmCallLogService
{
    /// <summary>Log an LLM call entry for a task.</summary>
    Task LogAsync(Guid taskId, LlmCallLogEntry entry);
    /// <summary>Get all LLM call logs for a task.</summary>
    Task<List<LlmCallLogEntry>> GetTaskCallLogsAsync(Guid taskId);
    /// <summary>Get aggregated token usage summary for a task.</summary>
    Task<TokenSummary> GetTokenSummaryAsync(Guid taskId);
}

/// <summary>A single LLM call log entry.</summary>
public class LlmCallLogEntry
{
    public int StepOrder { get; init; }
    public string CallType { get; init; } = string.Empty;
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public string? RequestPreview { get; init; }
    public string? ResponsePreview { get; init; }
    public int LatencyMs { get; init; }
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Aggregated token usage summary.</summary>
public class TokenSummary
{
    public int TotalPromptTokens { get; init; }
    public int TotalCompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public int CallCount { get; init; }
    public int ErrorCount { get; init; }
}
