using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("task_llm_call_logs")]
public class TaskLlmCallLog
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord Task { get; set; } = null!;

    public int StepOrder { get; set; }

    [SugarColumn(Length = 32)]
    public string CallType { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? Provider { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Model { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? RequestPreview { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ResponsePreview { get; set; }

    public int LatencyMs { get; set; }
    public bool IsError { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Tool Call 日志集合——记录每次工具调用的详情（JSON 格式）。
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ToolCallLogsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
