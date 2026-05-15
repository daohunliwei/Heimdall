namespace Heimdall.Core.Models;

/// <summary>
/// 模型分级配置——为 Wiki 生成的不同阶段指定不同模型。
/// </summary>
public class ModelTierConfig
{
    /// <summary>结构规划阶段使用的模型。</summary>
    public string? StructurePlanningModel { get; set; }

    /// <summary>页面生成阶段使用的模型。</summary>
    public string? PageGenerationModel { get; set; }

    /// <summary>质量审查阶段使用的模型。</summary>
    public string? QualityReviewModel { get; set; }

    /// <summary>默认 Provider。</summary>
    public string? DefaultProvider { get; set; }

    /// <summary>页面生成模型的最小推荐参数规模（B）。</summary>
    public static double MinRecommendedParamsB => 20;

    /// <summary>
    /// 判断给定模型是否低于推荐阈值。
    /// </summary>
    public static bool IsSmallModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return false;
        // 检测已知的小模型
        var lower = modelName.ToLowerInvariant();
        return lower switch
        {
            var m when m.Contains("7b") || m.Contains("8b") => true,
            var m when m.Contains("13b") || m.Contains("14b") => true,
            var m when m.Contains("gemma") && !m.Contains("gemma3") => true,
            _ => false
        };
    }
}
