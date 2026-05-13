namespace Heimdall.Core.Entities;

public class TaskLlmCallLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskId { get; set; }
    public TaskRecord Task { get; set; } = null!;
    public int StepOrder { get; set; }
    public string CallType { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public string? RequestPreview { get; set; }
    public string? ResponsePreview { get; set; }
    public int LatencyMs { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
