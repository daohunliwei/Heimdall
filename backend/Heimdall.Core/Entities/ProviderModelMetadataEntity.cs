using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("provider_model_metadata")]
public class ProviderModelMetadataEntity
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(Length = 64)]
    public string ProviderKey { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string ModelName { get; set; } = string.Empty;

    [SugarColumn(Length = 32)]
    public string BillingType { get; set; } = "TokenPlan";

    public int MaxContextTokens { get; set; } = 128000;
    public int MaxOutputTokens { get; set; } = 8192;

    [SugarColumn(IsNullable = true)]
    public int? RateLimitPerMinute { get; set; }

    [SugarColumn(Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? InputTokenPrice { get; set; }

    [SugarColumn(Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? OutputTokenPrice { get; set; }

    [SugarColumn(Length = 10, DecimalDigits = 6, IsNullable = true)]
    public decimal? CallPrice { get; set; }

    public bool SupportsCaching { get; set; }
    public double ContextFillRatio { get; set; } = 0.65;
    public double ContextWarningThreshold { get; set; } = 0.90;
    public bool SupportsStreaming { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? RawEndpoint { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
