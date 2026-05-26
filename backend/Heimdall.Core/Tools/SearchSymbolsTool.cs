using System.ComponentModel;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Tools;

/// <summary>
/// 符号搜索工具——LLM 通过 Tool Call 在代码索引中搜索类/接口/方法等符号。
/// </summary>
public static class SearchSymbolsTool
{
    /// <summary>
    /// 在代码索引中搜索指定的符号名称，返回 top-10 匹配结果。
    /// </summary>
    [Description("在代码索引中搜索指定的符号名称（类、接口、方法、属性），返回文件路径和行号。结果最多10条。")]
    public static string SearchSymbols(
        IHybridSearchService hybridSearch,
        string indexKey,
        string query,
        string? symbolKind = null)
    {
        var results = hybridSearch.SearchAsync(indexKey, query).GetAwaiter().GetResult();

        var filtered = results
            .Where(r => string.IsNullOrEmpty(symbolKind) || r.FilePath.Contains($".{symbolKind}", StringComparison.OrdinalIgnoreCase) is false)
            .Take(10)
            .ToList();

        if (filtered.Count == 0)
        {
            return $"搜索 \"{query}\" 无结果。建议尝试不同的搜索词或使用更短的关键词。";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"搜索 \"{query}\" 的结果（共 {filtered.Count} 条）：");
        foreach (var r in filtered)
        {
            sb.AppendLine($"- {r.FilePath}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建 AIFunction 实例，捕获 IHybridSearchService 上下文。
    /// </summary>
    public static AIFunction Create(IHybridSearchService hybridSearch, string indexKey) =>
        AIFunctionFactory.Create(
            (string query) => SearchSymbols(hybridSearch, indexKey, query),
            name: "SearchSymbols",
            description: "在代码索引中搜索指定的符号名称，返回匹配的文件路径。结果最多10条。");
}
