using System.ComponentModel;
using Heimdall.Core.Models;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Tools;

/// <summary>
/// 类定义检索工具——LLM 通过 Tool Call 获取指定类的完整签名、基类、方法和属性。
/// </summary>
public static class RetrieveClassDefinitionTool
{
    /// <summary>
    /// 从代码索引中检索指定类的完整定义信息。
    /// </summary>
    /// <param name="codeIndex">代码索引条目列表</param>
    /// <param name="className">要检索的类名</param>
    [Description("从代码索引中检索指定类的完整签名、导出符号和文件信息。参数 className 为要查询的类名。")]
    public static string RetrieveClassDefinition(
        List<CodeIndexEntry> codeIndex,
        string className)
    {
        var matches = codeIndex
            .Where(e => e.ExportedSymbols.Any(s => s.Contains(className, StringComparison.OrdinalIgnoreCase)))
            .Take(5)
            .ToList();

        if (matches.Count == 0)
        {
            return $"未在代码索引中找到类 \"{className}\" 的定义。请检查类名拼写或使用 SearchSymbols 搜索。";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"检索 \"{className}\" 的结果（共 {matches.Count} 条）：");

        foreach (var entry in matches)
        {
            sb.AppendLine($"- 文件: {entry.FilePath}");
            sb.AppendLine($"  模块: {entry.ModuleName}");
            sb.AppendLine($"  语言: {entry.Language}");
            sb.AppendLine($"  导出符号: {string.Join(", ", entry.ExportedSymbols)}");
            if (entry.DependencyHints.Count > 0)
                sb.AppendLine($"  依赖提示: {string.Join(", ", entry.DependencyHints.Take(10))}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建 AIFunction 实例，捕获代码索引数据上下文。
    /// </summary>
    public static AIFunction Create(List<CodeIndexEntry> codeIndex) =>
        AIFunctionFactory.Create(
            (string className) => RetrieveClassDefinition(codeIndex, className),
            name: "RetrieveClassDefinition",
            description: "从代码索引中检索指定类的完整签名、导出符号和文件信息。");
}
