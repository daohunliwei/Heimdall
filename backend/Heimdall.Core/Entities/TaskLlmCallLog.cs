using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("task_llm_call_logs")]
public class TaskLlmCallLog
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "TaskId")]
    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord Task { get; set; } = null!;

    [SugarColumn(ColumnName = "StepOrder")]
    public int StepOrder { get; set; }

    [SugarColumn(ColumnName = "CallType", Length = 32)]
    public string CallType { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Provider", Length = 32, IsNullable = true)]
    public string? Provider { get; set; }

    [SugarColumn(ColumnName = "Model", Length = 64, IsNullable = true)]
    public string? Model { get; set; }

    [SugarColumn(ColumnName = "PromptTokens")]
    public int PromptTokens { get; set; }

    [SugarColumn(ColumnName = "CompletionTokens")]
    public int CompletionTokens { get; set; }

    [SugarColumn(ColumnName = "TotalTokens")]
    public int TotalTokens { get; set; }

    [SugarColumn(ColumnName = "RequestPreview", ColumnDataType = "text", IsNullable = true)]
    public string? RequestPreview { get; set; }

    [SugarColumn(ColumnName = "ResponsePreview", ColumnDataType = "text", IsNullable = true)]
    public string? ResponsePreview { get; set; }

    [SugarColumn(ColumnName = "LatencyMs")]
    public int LatencyMs { get; set; }

    [SugarColumn(ColumnName = "IsError")]
    public bool IsError { get; set; }

    [SugarColumn(ColumnName = "ErrorMessage", ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "ToolCallLogsJson", ColumnDataType = "text", IsNullable = true)]
    public string? ToolCallLogsJson { get; set; }

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
