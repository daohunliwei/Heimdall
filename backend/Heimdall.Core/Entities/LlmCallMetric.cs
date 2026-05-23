using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("llm_call_metrics")]
public class LlmCallMetric
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord? Task { get; set; }

    [SugarColumn(Length = 64)]
    public string Stage { get; set; } = string.Empty;

    [SugarColumn(Length = 32)]
    public string Provider { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheHitTokens { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; } = true;

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ErrorType { get; set; }

    public bool IsEstimated { get; set; }
    public bool IsStreaming { get; set; }
    public int? FirstTokenLatencyMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
