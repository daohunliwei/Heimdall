using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Infrastructure.Models;

/// <summary>
/// Provider 计费类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingType
{
    /// <summary>按调用次数收费（如本地 Ollama、DeepSeek Pro 按次计费套餐）。</summary>
    CodingPlan,
    /// <summary>按 Token 用量收费（如 OpenAI、Google、Azure）。</summary>
    TokenPlan
}

/// <summary>
/// Provider/Model 组合的计费与能力元数据。
/// </summary>
public class ProviderModelMetadata
{
    /// <summary>计费类型：CodingPlan（按次）或 TokenPlan（按量）。</summary>
    public BillingType BillingType { get; set; } = BillingType.TokenPlan;

    /// <summary>模型上下文窗口大小（Token 数）。</summary>
    public int MaxContextTokens { get; set; } = 128000;

    /// <summary>最大输出 Token 数。</summary>
    public int MaxOutputTokens { get; set; } = 8192;

    /// <summary>速率限制（次/分钟），null 表示无限制。</summary>
    public int? RateLimitPerMinute { get; set; }

    /// <summary>输入 Token 价格（每百万 Token），仅 TokenPlan 有效。</summary>
    public decimal? InputTokenPrice { get; set; }

    /// <summary>输出 Token 价格（每百万 Token），仅 TokenPlan 有效。</summary>
    public decimal? OutputTokenPrice { get; set; }

    /// <summary>单次调用价格，仅 CodingPlan 有效。</summary>
    public decimal? CallPrice { get; set; }

    /// <summary>是否支持 prompt 缓存（如 OpenAI cached_tokens）。</summary>
    public bool SupportsCaching { get; set; }

    /// <summary>上下文填充比例（0-1），默认 0.65。决定单次调用最多使用上下文窗口的多少。</summary>
    public double ContextFillRatio { get; set; } = 0.65;

    /// <summary>上下文警戒阈值（0-1），默认 0.90。超过此比例时输出警告并截断低优先级内容。</summary>
    public double ContextWarningThreshold { get; set; } = 0.90;
}

/// <summary>
/// LLM 调用的统一响应模型，所有 Provider 适配后返回此结构。
/// </summary>
public class ChatCompletionResponse
{
    /// <summary>生成的文本内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Token 用量统计。</summary>
    public TokenUsage Usage { get; set; } = new();

    /// <summary>结束原因（stop/length/error）。</summary>
    public string? FinishReason { get; set; }

    /// <summary>调用耗时（毫秒）。</summary>
    public int LatencyMs { get; set; }
}

/// <summary>
/// Token 用量统计。
/// </summary>
public class TokenUsage
{
    /// <summary>输入 Token 数。</summary>
    public int InputTokens { get; set; }

    /// <summary>输出 Token 数。</summary>
    public int OutputTokens { get; set; }

    /// <summary>缓存命中 Token 数。</summary>
    public int CacheHitTokens { get; set; }

    /// <summary>是否为估算值（Provider 未返回 usage 时使用 TokenCounter 估算）。</summary>
    public bool IsEstimated { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatCompletionRequest
{
    public string RepoUrl { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public string? FilePath { get; set; }
    public string? Token { get; set; }
    public string? Type { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? CustomModel { get; set; }
    public string? Language { get; set; }
    public string? ExcludedDirs { get; set; }
    public string? ExcludedFiles { get; set; }
    public string? IncludedDirs { get; set; }
    public string? IncludedFiles { get; set; }
}

public class ProviderChatRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public int MaxOutputTokens { get; set; }
    public Dictionary<string, JsonElement>? Options { get; set; }
}
