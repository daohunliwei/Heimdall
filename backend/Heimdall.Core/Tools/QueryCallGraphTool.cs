using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Tools;

/// <summary>
/// 调用图查询工具——LLM 通过 Tool Call 查询指定符号的调用者和被调用者关系。
/// </summary>
public static class QueryCallGraphTool
{
    /// <summary>
    /// 查询指定符号的调用图关系。
    /// </summary>
    /// <param name="callGraph">调用图数据（Dictionary: 符号名 → 调用关系）</param>
    /// <param name="symbolName">要查询的符号名称</param>
    /// <param name="direction">查询方向：callers（谁调用了它）、callees（它调用了谁）、both（双向）</param>
    [Description("查询代码调用图中指定符号的调用关系。direction: callers=谁调用了它, callees=它调用了谁, both=双向。")]
    public static string QueryCallGraph(
        Dictionary<string, (List<string> callers, List<string> callees)> callGraph,
        string symbolName,
        string direction = "both")
    {
        if (!callGraph.TryGetValue(symbolName, out var relations))
        {
            return $"符号 \"{symbolName}\" 不在调用图索引中。可能是无调用关系的叶子方法，或使用 SearchSymbols 确认符号名拼写。";
        }

        var sb = new System.Text.StringBuilder();

        if (direction is "callers" or "both")
        {
            sb.AppendLine($"## 调用 \"{symbolName}\" 的符号 ({relations.callers.Count} 个)：");
            foreach (var caller in relations.callers.Take(20))
            {
                sb.AppendLine($"- {caller}");
            }
            if (relations.callers.Count > 20)
                sb.AppendLine($"  ... 及其他 {relations.callers.Count - 20} 个调用者");
        }

        if (direction is "callees" or "both")
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"## \"{symbolName}\" 调用的符号 ({relations.callees.Count} 个)：");
            foreach (var callee in relations.callees.Take(20))
            {
                sb.AppendLine($"- {callee}");
            }
            if (relations.callees.Count > 20)
                sb.AppendLine($"  ... 及其他 {relations.callees.Count - 20} 个被调用者");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建 AIFunction 实例，捕获调用图数据上下文。
    /// </summary>
    public static AIFunction Create(Dictionary<string, (List<string> callers, List<string> callees)> callGraph) =>
        AIFunctionFactory.Create(
            (string symbolName, string direction = "both") => QueryCallGraph(callGraph, symbolName, direction),
            name: "QueryCallGraph",
            description: "查询代码调用图中指定符号的调用者和被调用者关系。direction: callers/callees/both。");
}
