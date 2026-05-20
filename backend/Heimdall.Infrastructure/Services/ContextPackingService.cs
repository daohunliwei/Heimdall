using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Utilities;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// 上下文打包服务——根据 Provider/Model 元数据和填充比例，
/// 将多段内容智能打包到单次调用的上下文窗口中。
/// </summary>
public sealed class ContextPackingService
{
    private readonly HeimdallConfigService _configService;

    public ContextPackingService(HeimdallConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 计算给定 Provider/Model 组合下可用于内容的 Token 预算。
    /// 公式：MaxContextTokens × FillRatio - SystemPromptTokens - OutputReserve
    /// </summary>
    public int CalculateAvailableBudget(string provider, string model, string? systemPrompt = null, int? outputReserve = null)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);
        var fillRatio = _configService.GetContextFillRatio();

        var totalBudget = (int)(metadata.MaxContextTokens * fillRatio);
        var systemTokens = TokenCounter.EstimateTokenCount(systemPrompt);
        var reserve = outputReserve ?? metadata.MaxOutputTokens;

        return Math.Max(0, totalBudget - systemTokens - reserve);
    }

    /// <summary>
    /// 将多个内容段按优先级打包到上下文预算内。
    /// 高优先级的内容先放入，超出预算的低优先级内容被截断或丢弃。
    /// 返回打包后的内容列表和实际 Token 使用量。
    /// </summary>
    public ContextPackResult Pack(IEnumerable<ContextSegment> segments, int tokenBudget)
    {
        var result = new ContextPackResult { TokenBudget = tokenBudget };
        var remaining = tokenBudget;

        foreach (var segment in segments.OrderByDescending(s => s.Priority))
        {
            if (remaining <= 0) break;

            var segmentTokens = TokenCounter.EstimateTokenCount(segment.Content);

            if (segmentTokens <= remaining)
            {
                result.PackedSegments.Add(segment);
                remaining -= segmentTokens;
            }
            else if (segment.AllowTruncation && remaining > 100)
            {
                var truncated = TokenCounter.TruncateToTokenLimit(segment.Content, remaining);
                result.PackedSegments.Add(segment with { Content = truncated, WasTruncated = true });
                remaining -= TokenCounter.EstimateTokenCount(truncated);
            }
            else
            {
                result.DroppedSegments.Add(segment);
            }
        }

        result.UsedTokens = tokenBudget - remaining;
        result.FillRate = tokenBudget > 0 ? (double)result.UsedTokens / tokenBudget : 0;
        return result;
    }

    /// <summary>
    /// 将打包结果组装为最终的 prompt 文本。
    /// </summary>
    public string AssemblePrompt(ContextPackResult packResult, string? separator = null)
    {
        separator ??= "\n\n---\n\n";
        return string.Join(separator, packResult.PackedSegments
            .OrderByDescending(s => s.Priority)
            .Select(s => s.Content));
    }
}

/// <summary>
/// 待打包的上下文段落。
/// </summary>
public record ContextSegment
{
    /// <summary>内容文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>优先级（越高越优先放入上下文）。</summary>
    public int Priority { get; init; }

    /// <summary>段落标签（用于调试和日志）。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>是否允许截断。</summary>
    public bool AllowTruncation { get; init; } = true;

    /// <summary>是否被截断（打包后由系统设置）。</summary>
    public bool WasTruncated { get; init; }
}

/// <summary>
/// 上下文打包结果。
/// </summary>
public class ContextPackResult
{
    /// <summary>Token 预算。</summary>
    public int TokenBudget { get; set; }

    /// <summary>实际使用的 Token 数。</summary>
    public int UsedTokens { get; set; }

    /// <summary>填充率（0-1）。</summary>
    public double FillRate { get; set; }

    /// <summary>成功打包的段落列表。</summary>
    public List<ContextSegment> PackedSegments { get; set; } = new();

    /// <summary>因预算不足而丢弃的段落。</summary>
    public List<ContextSegment> DroppedSegments { get; set; } = new();
}
