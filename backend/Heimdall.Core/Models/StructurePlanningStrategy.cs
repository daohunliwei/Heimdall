namespace Heimdall.Core.Models;

/// <summary>
/// 结构规划策略
/// </summary>
public enum StructurePlanningStrategy
{
    /// <summary>确定性算法：基于代码索引直接生成，零成本零延迟</summary>
    Deterministic,

    /// <summary>LLM JSON：当前行为，LLM 生成 JSON 后解析</summary>
    LlmJson,

    /// <summary>LLM 增强：算法骨架 + 逐 Section LLM 润色标题/描述</summary>
    LlmEnhanced
}

/// <summary>
/// LLM 润色 Section 的返回结构
/// </summary>
public class SectionPolishResult
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}
