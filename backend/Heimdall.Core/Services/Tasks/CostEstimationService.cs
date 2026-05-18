using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 成本估算服务——根据仓库规模和模型选择预估 LLM 调用成本。
/// 价格基于 2026 年公开定价（可配置）。
/// </summary>
public sealed class CostEstimationService
{
    private readonly ILogger<CostEstimationService> _logger;

    public CostEstimationService(ILogger<CostEstimationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 估算一次 Wiki 生成的 Token 消耗和预估成本。
    /// </summary>
    public CostEstimate Estimate(int sourceFileCount, int estimatedPageCount, string? provider, string? model)
    {
        // 结构规划：~2000 input tokens
        var structureInputTokens = 2000L;
        var structureOutputTokens = 1000L;

        // 每页生成：检索的代码片段 ~5000 tokens + 页面说明 ~500 tokens
        var perPageInputTokens = 5500L;
        var perPageOutputTokens = 3000L;

        var totalInputTokens = structureInputTokens + perPageInputTokens * estimatedPageCount;
        var totalOutputTokens = structureOutputTokens + perPageOutputTokens * estimatedPageCount;

        var (inputPrice, outputPrice) = GetPricePerMTok(provider, model);
        var estimatedCost = (totalInputTokens / 1_000_000.0) * inputPrice
                          + (totalOutputTokens / 1_000_000.0) * outputPrice;

        _logger.LogInformation("成本估算：{Pages} 页, 输入 {InputK}K tokens, 输出 {OutputK}K tokens, 预估 ${Cost:F2}",
            estimatedPageCount, totalInputTokens / 1000, totalOutputTokens / 1000, estimatedCost);

        return new CostEstimate
        {
            EstimatedInputTokens = totalInputTokens,
            EstimatedOutputTokens = totalOutputTokens,
            EstimatedCallCount = 1 + estimatedPageCount, // 结构规划 + 每页一次
            EstimatedCostUsd = estimatedCost,
            Provider = provider ?? "ollama",
            Model = model ?? "default"
        };
    }

    private static (double inputPrice, double outputPrice) GetPricePerMTok(string? provider, string? model)
    {
        provider = provider?.ToLowerInvariant();

        if (provider == "openai" || provider?.Contains("openai") == true)
        {
            if (model?.Contains("gpt-4o-mini") == true) return (0.15, 0.60);
            if (model?.Contains("gpt-4o") == true) return (2.50, 10.00);
            return (2.50, 10.00);
        }

        if (provider == "deepseek" || provider?.Contains("deepseek") == true)
            return (0.27, 1.10);

        if (provider == "google" || provider?.Contains("google") == true)
            return (0.15, 0.60);

        // Ollama 本地：零成本
        if (provider == "ollama")
            return (0, 0);

        // 默认估算
        return (0.50, 2.00);
    }
}

public class CostEstimate
{
    public long EstimatedInputTokens { get; init; }
    public long EstimatedOutputTokens { get; init; }
    public int EstimatedCallCount { get; init; }
    public double EstimatedCostUsd { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
