using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("provider_model_metadata")]
public class ProviderModelMetadataEntity
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "ProviderKey", Length = 64)]
    public string ProviderKey { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "ModelName", Length = 128)]
    public string ModelName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "BillingType", Length = 32)]
    public string BillingType { get; set; } = "TokenPlan";

    [SugarColumn(ColumnName = "MaxContextTokens")]
    public int MaxContextTokens { get; set; } = 128000;

    [SugarColumn(ColumnName = "MaxOutputTokens")]
    public int MaxOutputTokens { get; set; } = 8192;

    [SugarColumn(ColumnName = "RateLimitPerMinute", IsNullable = true)]
    public int? RateLimitPerMinute { get; set; }

    [SugarColumn(ColumnName = "InputTokenPrice", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? InputTokenPrice { get; set; }

    [SugarColumn(ColumnName = "OutputTokenPrice", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? OutputTokenPrice { get; set; }

    [SugarColumn(ColumnName = "CallPrice", Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? CallPrice { get; set; }

    [SugarColumn(ColumnName = "SupportsCaching")]
    public bool SupportsCaching { get; set; }

    [SugarColumn(ColumnName = "ContextFillRatio")]
    public double ContextFillRatio { get; set; } = 0.65;

    [SugarColumn(ColumnName = "ContextWarningThreshold")]
    public double ContextWarningThreshold { get; set; } = 0.90;

    [SugarColumn(ColumnName = "SupportsStreaming")]
    public bool SupportsStreaming { get; set; } = true;

    [SugarColumn(ColumnName = "RawEndpoint", IsNullable = true)]
    public string? RawEndpoint { get; set; }

    [SugarColumn(ColumnName = "UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
