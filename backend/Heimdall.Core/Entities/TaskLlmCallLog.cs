using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("task_llm_call_logs")]
public class TaskLlmCallLog
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "task_id")]
    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord Task { get; set; } = null!;

    [SugarColumn(ColumnName = "step_order")]
    public int StepOrder { get; set; }

    [SugarColumn(ColumnName = "call_type", Length = 32)]
    public string CallType { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "provider", Length = 32, IsNullable = true)]
    public string? Provider { get; set; }

    [SugarColumn(ColumnName = "model", Length = 64, IsNullable = true)]
    public string? Model { get; set; }

    [SugarColumn(ColumnName = "prompt_tokens")]
    public int PromptTokens { get; set; }

    [SugarColumn(ColumnName = "completion_tokens")]
    public int CompletionTokens { get; set; }

    [SugarColumn(ColumnName = "total_tokens")]
    public int TotalTokens { get; set; }

    [SugarColumn(ColumnName = "request_preview", ColumnDataType = "text", IsNullable = true)]
    [Obsolete("已迁移到 Workspace 文件系统，请使用 log_file_path 定位 JSONL 日志文件")]
    public string? RequestPreview { get; set; }

    [SugarColumn(ColumnName = "response_preview", ColumnDataType = "text", IsNullable = true)]
    [Obsolete("已迁移到 Workspace 文件系统，请使用 log_file_path 定位 JSONL 日志文件")]
    public string? ResponsePreview { get; set; }

    [SugarColumn(ColumnName = "log_file_path", Length = 512, IsNullable = true)]
    public string? LogFilePath { get; set; }

    [SugarColumn(ColumnName = "latency_ms")]
    public int LatencyMs { get; set; }

    [SugarColumn(ColumnName = "is_error")]
    public bool IsError { get; set; }

    [SugarColumn(ColumnName = "error_message", ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "tool_call_logs_json", ColumnDataType = "text", IsNullable = true)]
    public string? ToolCallLogsJson { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
