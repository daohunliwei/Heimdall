using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("llm_call_metrics")]
public class LlmCallMetric
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "TaskId")]
    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord? Task { get; set; }

    [SugarColumn(ColumnName = "Stage", Length = 64)]
    public string Stage { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Provider", Length = 32)]
    public string Provider { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Model", Length = 64)]
    public string Model { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "InputTokens")]
    public int InputTokens { get; set; }

    [SugarColumn(ColumnName = "OutputTokens")]
    public int OutputTokens { get; set; }

    [SugarColumn(ColumnName = "CacheHitTokens")]
    public int CacheHitTokens { get; set; }

    [SugarColumn(ColumnName = "LatencyMs")]
    public int LatencyMs { get; set; }

    [SugarColumn(ColumnName = "Success")]
    public bool Success { get; set; } = true;

    [SugarColumn(ColumnName = "ErrorType", Length = 64, IsNullable = true)]
    public string? ErrorType { get; set; }

    [SugarColumn(ColumnName = "IsEstimated")]
    public bool IsEstimated { get; set; }

    [SugarColumn(ColumnName = "IsStreaming")]
    public bool IsStreaming { get; set; }

    [SugarColumn(ColumnName = "FirstTokenLatencyMs", IsNullable = true)]
    public int? FirstTokenLatencyMs { get; set; }

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
