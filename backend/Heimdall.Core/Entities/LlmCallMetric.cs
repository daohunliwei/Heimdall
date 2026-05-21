namespace Heimdall.Core.Entities;

/// <summary>
/// LLM 调用指标实体——记录每次 LLM 调用的 Token 消耗和性能数据。
/// </summary>
public class LlmCallMetric
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskId { get; set; }
    public TaskRecord? Task { get; set; }

    /// <summary>管线阶段名（structure_planning/page_generation/quality_assurance/code_understanding 等）。</summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>Provider 标识（ollama/openai/google 等）。</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>模型名称。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>输入 Token 数。</summary>
    public int InputTokens { get; set; }

    /// <summary>输出 Token 数。</summary>
    public int OutputTokens { get; set; }

    /// <summary>缓存命中 Token 数。</summary>
    public int CacheHitTokens { get; set; }

    /// <summary>调用耗时（毫秒）。</summary>
    public int LatencyMs { get; set; }

    /// <summary>是否成功。</summary>
    public bool Success { get; set; } = true;

    /// <summary>错误类型（Timeout/RateLimit/ServerError 等）。</summary>
    public string? ErrorType { get; set; }

    /// <summary>Token 数据是否为估算值。</summary>
    public bool IsEstimated { get; set; }

    /// <summary>记录时间。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
