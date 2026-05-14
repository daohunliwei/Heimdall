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
    /// <summary>V2: 目标分支</summary>
    public string? TargetBranch { get; set; }
    /// <summary>V2: 实际生成使用的仓库快照版本</summary>
    public Guid? ResolvedRepositoryVersionId { get; set; }
    public RepositoryVersion? ResolvedRepositoryVersion { get; set; }
    /// <summary>V2: 生成的 Wiki 版本</summary>
    public Guid? ResultWikiVersionId { get; set; }
    public WikiVersion? ResultWikiVersion { get; set; }
    /// <summary>V2: 刷新策略 current / latest</summary>
    public string? RefreshStrategy { get; set; }
    /// <summary>V2: 是否强制刷新</summary>
    public bool ForceRefresh { get; set; }
    /// <summary>V2: 生成配置摘要</summary>
    public string? ConfigHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<TaskLlmCallLog> LlmCallLogs { get; set; } = new List<TaskLlmCallLog>();
    public ICollection<WikiPage> WikiPages { get; set; } = new List<WikiPage>();
}
