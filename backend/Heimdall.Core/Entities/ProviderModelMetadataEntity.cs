namespace Heimdall.Core.Entities;

public class ProviderModelMetadataEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ProviderKey { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string BillingType { get; set; } = "TokenPlan";

    public int MaxContextTokens { get; set; } = 128000;

    public int MaxOutputTokens { get; set; } = 8192;

    public int? RateLimitPerMinute { get; set; }

    public decimal? InputTokenPrice { get; set; }

    public decimal? OutputTokenPrice { get; set; }

    public decimal? CallPrice { get; set; }

    public bool SupportsCaching { get; set; }

    public double ContextFillRatio { get; set; } = 0.65;

    public double ContextWarningThreshold { get; set; } = 0.90;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
