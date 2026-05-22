using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("task_artifacts")]
public class TaskArtifact
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord Task { get; set; } = null!;

    [SugarColumn(ColumnName = "artifact_type", Length = 64)]
    public string ArtifactType { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "artifact_key", Length = 128)]
    public string ArtifactKey { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "stage_name", Length = 64)]
    public string StageName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; set; } = "completed";

    [SugarColumn(ColumnName = "sequence")]
    public int Sequence { get; set; }

    [SugarColumn(ColumnName = "content_hash", Length = 64, IsNullable = true)]
    public string? ContentHash { get; set; }

    [SugarColumn(ColumnName = "summary", ColumnDataType = "text", IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(ColumnName = "payload_json", ColumnDataType = "jsonb")]
    public string PayloadJson { get; set; } = "{}";

    [SugarColumn(ColumnName = "error_message", ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
