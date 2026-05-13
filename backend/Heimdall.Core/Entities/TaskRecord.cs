namespace Heimdall.Core.Entities;

public class TaskRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TaskType { get; set; } = "wiki";
    public string Status { get; set; } = "pending";
    public Guid? RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public string SourceBranch { get; set; } = "main";
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Language { get; set; }
    public int ProgressPercent { get; set; }
    public string? ProgressMessage { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<TaskLlmCallLog> LlmCallLogs { get; set; } = new List<TaskLlmCallLog>();
    public ICollection<WikiPage> WikiPages { get; set; } = new List<WikiPage>();
}
