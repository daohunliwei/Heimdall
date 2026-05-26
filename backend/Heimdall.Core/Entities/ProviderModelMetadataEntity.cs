using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("provider_model_metadata")]
public class ProviderModelMetadataEntity
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "provider_key", Length = 64)]
    public string ProviderKey { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "model_name", Length = 128)]
    public string ModelName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "billing_type", Length = 32)]
    public string BillingType { get; set; } = "TokenPlan";

    [SugarColumn(ColumnName = "max_context_tokens")]
    public int MaxContextTokens { get; set; } = 128000;

    [SugarColumn(ColumnName = "max_output_tokens")]
    public int MaxOutputTokens { get; set; } = 8192;

    [SugarColumn(ColumnName = "rate_limit_per_minute", IsNullable = true)]
    public int? RateLimitPerMinute { get; set; }

    [SugarColumn(ColumnName = "input_token_price", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? InputTokenPrice { get; set; }

    [SugarColumn(ColumnName = "output_token_price", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? OutputTokenPrice { get; set; }

    [SugarColumn(ColumnName = "call_price", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? CallPrice { get; set; }

    [SugarColumn(ColumnName = "supports_caching")]
    public bool SupportsCaching { get; set; }

    [SugarColumn(ColumnName = "context_fill_ratio")]
    public double ContextFillRatio { get; set; } = 0.65;

    [SugarColumn(ColumnName = "context_warning_threshold")]
    public double ContextWarningThreshold { get; set; } = 0.90;

    [SugarColumn(ColumnName = "supports_streaming")]
    public bool SupportsStreaming { get; set; } = true;

    [SugarColumn(ColumnName = "raw_endpoint", IsNullable = true)]
    public string? RawEndpoint { get; set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
