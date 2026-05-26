using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("llm_call_metrics")]
public class LlmCallMetric
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "task_id")]
    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord? Task { get; set; }

    [SugarColumn(ColumnName = "stage", Length = 64)]
    public string Stage { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "provider", Length = 32)]
    public string Provider { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "model", Length = 64)]
    public string Model { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "input_tokens")]
    public int InputTokens { get; set; }

    [SugarColumn(ColumnName = "output_tokens")]
    public int OutputTokens { get; set; }

    [SugarColumn(ColumnName = "cache_hit_tokens")]
    public int CacheHitTokens { get; set; }

    [SugarColumn(ColumnName = "latency_ms")]
    public int LatencyMs { get; set; }

    [SugarColumn(ColumnName = "success")]
    public bool Success { get; set; } = true;

    [SugarColumn(ColumnName = "error_type", Length = 64, IsNullable = true)]
    public string? ErrorType { get; set; }

    [SugarColumn(ColumnName = "is_estimated")]
    public bool IsEstimated { get; set; }

    [SugarColumn(ColumnName = "is_streaming")]
    public bool IsStreaming { get; set; }

    [SugarColumn(ColumnName = "first_token_latency_ms", IsNullable = true)]
    public int? FirstTokenLatencyMs { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
