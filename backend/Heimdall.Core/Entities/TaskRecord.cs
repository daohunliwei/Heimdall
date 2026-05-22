using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("tasks")]
public class TaskRecord
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(Length = 16)]
    public string TaskType { get; set; } = "wiki";

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; set; } = "pending";

    public Guid? RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository? Repository { get; set; }

    [SugarColumn(ColumnName = "source_branch", Length = 128)]
    public string SourceBranch { get; set; } = "main";

    public Guid? UserId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(UserId))]
    public User? User { get; set; }

    [SugarColumn(Length = 64)]
    public string RequestHash { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Provider { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Model { get; set; }

    [SugarColumn(Length = 8, IsNullable = true)]
    public string? Language { get; set; }

    public int ProgressPercent { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ProgressMessage { get; set; }

    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }

    [SugarColumn(ColumnDataType = "jsonb", IsNullable = true)]
    public string? ResultJson { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "current_stage", Length = 64)]
    public string CurrentStage { get; set; } = "queued";

    [SugarColumn(ColumnName = "current_stage_status", Length = 16)]
    public string CurrentStageStatus { get; set; } = "pending";

    [SugarColumn(ColumnName = "last_successful_stage", Length = 64, IsNullable = true)]
    public string? LastSuccessfulStage { get; set; }

    [SugarColumn(ColumnName = "last_artifact_id", IsNullable = true)]
    public Guid? LastArtifactId { get; set; }

    [SugarColumn(ColumnName = "attempt_count")]
    public int AttemptCount { get; set; }

    [SugarColumn(ColumnName = "resume_count")]
    public int ResumeCount { get; set; }

    [SugarColumn(ColumnName = "auto_resume_fail_count")]
    public int AutoResumeFailCount { get; set; }

    [SugarColumn(ColumnName = "target_branch", Length = 128, IsNullable = true)]
    public string? TargetBranch { get; set; }

    [SugarColumn(ColumnName = "resolved_repository_version_id", IsNullable = true)]
    public Guid? ResolvedRepositoryVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(ResolvedRepositoryVersionId))]
    public RepositoryVersion? ResolvedRepositoryVersion { get; set; }

    [SugarColumn(ColumnName = "result_wiki_version_id", IsNullable = true)]
    public Guid? ResultWikiVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(ResultWikiVersionId))]
    public WikiVersion? ResultWikiVersion { get; set; }

    [SugarColumn(ColumnName = "refresh_strategy", Length = 16, IsNullable = true)]
    public string? RefreshStrategy { get; set; }

    [SugarColumn(ColumnName = "force_refresh")]
    public bool ForceRefresh { get; set; }

    [SugarColumn(ColumnName = "config_hash", Length = 64, IsNullable = true)]
    public string? ConfigHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Navigate(NavigateType.OneToMany, nameof(TaskLlmCallLog.TaskId))]
    public List<TaskLlmCallLog> LlmCallLogs { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiPage.TaskId))]
    public List<WikiPage> WikiPages { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(TaskArtifact.TaskId))]
    public List<TaskArtifact> Artifacts { get; set; } = new();
}
