namespace Heimdall.Core.Models;

public class LlmCallLogEntry
{
    public Guid TaskId { get; set; }
    public int StepOrder { get; set; }
    public string CallType { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public string? RequestPreview { get; set; }
    public string? ResponsePreview { get; set; }
    public int LatencyMs { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}
