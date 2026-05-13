namespace Heimdall.Core.Models;

public class TaskEnqueueRequest
{
    public Guid? RepositoryId { get; set; }
    public string TaskType { get; set; } = "wiki";
    public string SourceBranch { get; set; } = "main";
    public Guid? UserId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Language { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string? CustomModel { get; set; }
    public bool ForceRefresh { get; set; }
    public bool Comprehensive { get; set; } = true;
    public string? Question { get; set; }
    public List<ChatMessage>? History { get; set; }
    public bool DeepResearch { get; set; }
    public string? FilePath { get; set; }
}
