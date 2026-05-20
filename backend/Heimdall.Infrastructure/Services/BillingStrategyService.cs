using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// 计费策略服务——根据 Provider 计费类型决定调用策略。
/// CodingPlan：减少调用次数，单次上下文吃满。
/// TokenPlan：按需调用，仅处理速率限制。
/// </summary>
public sealed class BillingStrategyService
{
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<BillingStrategyService> _logger;

    public BillingStrategyService(HeimdallConfigService configService, ILogger<BillingStrategyService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 获取给定 Provider/Model 的调用策略建议。
    /// </summary>
    public CallStrategy GetStrategy(string provider, string model)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);
        var fillRatio = _configService.GetContextFillRatio();

        return metadata.BillingType switch
        {
            BillingType.CodingPlan => new CallStrategy
            {
                BillingType = BillingType.CodingPlan,
                ShouldBatchContent = true,
                TargetFillRatio = Math.Max(fillRatio, 0.70),
                MaxConcurrentCalls = 1,
                PreferLargeContext = true,
                Description = $"按次计费模式：尽量减少调用次数，上下文填充 ≥{fillRatio:P0}"
            },
            BillingType.TokenPlan => new CallStrategy
            {
                BillingType = BillingType.TokenPlan,
                ShouldBatchContent = false,
                TargetFillRatio = fillRatio,
                MaxConcurrentCalls = metadata.RateLimitPerMinute ?? 10,
                PreferLargeContext = false,
                Description = $"按量计费模式：可并发调用（限速 {metadata.RateLimitPerMinute}/min），上下文填充 ~{fillRatio:P0}"
            },
            _ => new CallStrategy
            {
                BillingType = metadata.BillingType,
                TargetFillRatio = fillRatio
            }
        };
    }

    /// <summary>
    /// 根据计费策略，决定一组待处理项如何分批调用 LLM。
    /// CodingPlan: 尽量合并为少数大批次。
    /// TokenPlan: 每项独立调用或按合理大小分组。
    /// </summary>
    public List<List<T>> CreateBatches<T>(
        IList<T> items,
        Func<T, int> tokenEstimator,
        string provider, string model)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);
        var strategy = GetStrategy(provider, model);
        var maxContentTokens = (int)(metadata.MaxContextTokens * strategy.TargetFillRatio) - metadata.MaxOutputTokens;

        if (maxContentTokens <= 0) maxContentTokens = 4000;

        var batches = new List<List<T>>();
        var currentBatch = new List<T>();
        var currentTokens = 0;

        foreach (var item in items)
        {
            var itemTokens = tokenEstimator(item);

            if (strategy.ShouldBatchContent)
            {
                // CodingPlan: 尽量合并
                if (currentTokens + itemTokens > maxContentTokens && currentBatch.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<T>();
                    currentTokens = 0;
                }
                currentBatch.Add(item);
                currentTokens += itemTokens;
            }
            else
            {
                // TokenPlan: 按合理大小分组（不超过上下文的 80%）
                if (currentTokens + itemTokens > maxContentTokens * 0.8 && currentBatch.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<T>();
                    currentTokens = 0;
                }
                currentBatch.Add(item);
                currentTokens += itemTokens;
            }
        }

        if (currentBatch.Count > 0)
            batches.Add(currentBatch);

        _logger.LogInformation(
            "批次规划 Provider={Provider} Model={Model} BillingType={Billing} Items={Items} Batches={Batches}",
            provider, model, metadata.BillingType, items.Count, batches.Count);

        return batches;
    }
}

/// <summary>
/// LLM 调用策略。
/// </summary>
public class CallStrategy
{
    /// <summary>计费类型。</summary>
    public BillingType BillingType { get; set; }

    /// <summary>是否应该将内容合并为大批次。</summary>
    public bool ShouldBatchContent { get; set; }

    /// <summary>目标上下文填充率。</summary>
    public double TargetFillRatio { get; set; } = 0.65;

    /// <summary>最大并发调用数。</summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>是否偏好大上下文单次调用。</summary>
    public bool PreferLargeContext { get; set; }

    /// <summary>策略描述（用于日志/调试）。</summary>
    public string Description { get; set; } = string.Empty;
}
