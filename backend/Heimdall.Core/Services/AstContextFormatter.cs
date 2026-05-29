using System.Text;
using Heimdall.Infrastructure.AstAnalysis;

namespace Heimdall.Core.Services;

/// <summary>
/// 将 AST 结构化数据格式化为提示词可注入的 Markdown 文本
/// </summary>
public sealed class AstContextFormatter
{
    /// <summary>
    /// 将 AST 类型层级格式化为可读 Markdown
    /// 输出：类名、Kind、继承/实现关系、修饰符、公开方法签名
    /// </summary>
    public string FormatTypeHierarchy(IReadOnlyList<AstSymbol> symbols)
    {
        if (symbols.Count == 0)
        {
            return "（未提取到 AST 符号）";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## 类型层级（AST 分析）");

        var types = symbols
            .Where(s => s.Kind is "class" or "interface" or "struct" or "record" or "enum")
            .GroupBy(s => s.FilePath)
            .OrderBy(g => g.Key);

        foreach (var fileGroup in types)
        {
            sb.AppendLine();
            sb.AppendLine($"### `{fileGroup.Key}`");

            foreach (var type in fileGroup.OrderBy(t => t.Name))
            {
                var modifiers = type.Modifiers is { Length: > 0 }
                    ? string.Join(", ", type.Modifiers)
                    : "";
                var modifiersStr = modifiers.Length > 0 ? $"({modifiers}) " : "";

                var inherits = "";
                if (!string.IsNullOrWhiteSpace(type.ParentClass))
                {
                    inherits = $" 继承 `{type.ParentClass}`";
                }

                var implements = type.BaseTypes is { Length: > 0 }
                    ? $" 实现 {string.Join(", ", type.BaseTypes.Select(b => $"`{b}`"))}"
                    : "";

                sb.AppendLine($"- `{type.Name}` ({type.Kind}, {modifiersStr}行 {type.StartLine}-{type.EndLine}){inherits}{implements}");

                var methods = symbols
                    .Where(s => s.Kind is "method" or "function" or "constructor"
                        && s.FilePath == type.FilePath
                        && s.ParentClass == type.Name)
                    .OrderBy(m => m.Name)
                    .ToList();

                if (methods.Count > 0)
                {
                    sb.AppendLine($"  - {methods.Count} 个方法:");
                    foreach (var method in methods.Take(20))
                    {
                        var methodMods = method.Modifiers is { Length: > 0 }
                            ? string.Join(" ", method.Modifiers) + " "
                            : "";
                        sb.AppendLine($"    - {methodMods}`{method.FullSignature}`");
                    }

                    if (methods.Count > 20)
                    {
                        sb.AppendLine($"    - ...（共 {methods.Count} 个方法，仅显示前 20）");
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将 AST 调用边格式化为 "A → B → C" 调用链
    /// </summary>
    public string FormatCallTopology(IReadOnlyList<AstCallEdge> edges, int maxCallers = 30)
    {
        if (edges.Count == 0)
        {
            return "（未提取到调用关系）";
        }

        var directCalls = edges
            .Where(e => e.CallType == "direct" && !string.IsNullOrWhiteSpace(e.CallerSymbol))
            .ToList();

        if (directCalls.Count == 0)
        {
            return "（未提取到直接调用关系）";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## 调用拓扑（AST 分析）");

        var grouped = directCalls
            .GroupBy(e => e.CallerSymbol)
            .OrderByDescending(g => g.Count())
            .Take(maxCallers)
            .ToList();

        foreach (var callerGroup in grouped)
        {
            var callees = callerGroup
                .Select(e => e.CalleeSymbol)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .Take(10)
                .ToList();

            if (callees.Count == 0) continue;

            var calleeList = string.Join(", ", callees.Select(c => $"`{c}`"));
            var filePath = callerGroup.First().CallerFilePath;
            var confidence = callerGroup.Max(e => e.Confidence);
            sb.AppendLine($"- `{callerGroup.Key}` ({Path.GetFileName(filePath)}) → {calleeList} [置信度: {confidence:P0}]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将 AST 检测到的设计模式格式化为结构化 Markdown
    /// 输入格式: "PatternName|Confidence|FilePath|Detail"
    /// </summary>
    public string FormatDesignPatternEvidence(IReadOnlyList<string> patternHints)
    {
        if (patternHints.Count == 0)
        {
            return "（未检测到设计模式）";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## 设计模式（AST 检测）");

        foreach (var hint in patternHints)
        {
            var parts = hint.Split('|');
            if (parts.Length < 4) continue;

            var name = parts[0];
            var confidence = double.TryParse(parts[1], out var conf) ? conf : 0;
            var filePath = parts[2];
            var detail = parts[3];

            sb.AppendLine($"- **{name}** (置信度: {confidence:P0})");
            sb.AppendLine($"  - 文件: `{Path.GetFileName(filePath)}`");
            if (!string.IsNullOrWhiteSpace(detail))
            {
                sb.AppendLine($"  - 详情: {detail}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 为页面生成提示词中的单个代码块构建 AST L2 上下文
    /// 紧凑格式：Class → extends → implements | Signature | Called by → Calls | Design Role
    /// </summary>
    public string FormatPageCodeBlockContext(
        AstSymbol? symbol,
        IReadOnlyList<AstCallEdge> callEdges,
        IReadOnlyList<string> designPatternHints,
        bool compact = false)
    {
        if (symbol == null) return string.Empty;

        if (compact)
        {
            return CompactFormat(symbol);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"> **AST Context** | Class: `{symbol.Name}`");

        if (!string.IsNullOrWhiteSpace(symbol.ParentClass))
        {
            sb.Append($" (extends `{symbol.ParentClass}`");
            if (symbol.BaseTypes is { Length: > 0 })
            {
                sb.Append($", implements {string.Join(", ", symbol.BaseTypes.Select(b => $"`{b}`"))}");
            }
            sb.AppendLine(")");
        }
        else if (symbol.BaseTypes is { Length: > 0 })
        {
            sb.AppendLine($" (implements {string.Join(", ", symbol.BaseTypes.Select(b => $"`{b}`"))})");
        }
        else
        {
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(symbol.FullSignature))
        {
            sb.AppendLine($"> Signature: `{symbol.FullSignature}`");
        }

        var callers = callEdges
            .Where(e => e.CalleeSymbol == symbol.Name || e.CalleeSymbol == $"{symbol.ParentClass}.{symbol.Name}")
            .Select(e => e.CallerSymbol)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(5)
            .ToList();

        if (callers.Count > 0)
        {
            sb.AppendLine($"> Called by: {string.Join(", ", callers.Select(c => $"`{c}`"))}");
        }

        var callsOut = callEdges
            .Where(e => e.CallerSymbol == symbol.Name || e.CallerSymbol == $"{symbol.ParentClass}.{symbol.Name}")
            .Select(e => e.CalleeSymbol)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Take(5)
            .ToList();

        if (callsOut.Count > 0)
        {
            sb.AppendLine($"> Calls: {string.Join(", ", callsOut.Select(c => $"`{c}`"))}");
        }

        if (designPatternHints.Count > 0 && !string.IsNullOrWhiteSpace(symbol.ParentClass))
        {
            var relevant = designPatternHints
                .Where(h => h.Contains(symbol.ParentClass, StringComparison.OrdinalIgnoreCase))
                .Select(h =>
                {
                    var parts = h.Split('|');
                    return parts.Length > 0 ? parts[0] : "";
                })
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList();

            if (relevant.Count > 0)
            {
                sb.AppendLine($"> Design Role: {string.Join(", ", relevant)} participant");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 高重要性代码块完整上下文，低重要性折叠为单行
    /// </summary>
    public string FormatPageCodeBlockContextWithFoldStrategy(
        AstSymbol? symbol,
        IReadOnlyList<AstCallEdge> callEdges,
        IReadOnlyList<string> designPatternHints,
        int importance)
    {
        if (symbol == null) return string.Empty;

        if (importance < 5)
        {
            return CompactFormat(symbol);
        }

        return FormatPageCodeBlockContext(symbol, callEdges, designPatternHints, compact: false);
    }

    private static string CompactFormat(AstSymbol symbol)
    {
        var mods = symbol.Modifiers is { Length: > 0 }
            ? $"({string.Join(", ", symbol.Modifiers)})"
            : "";
        return $"> **AST**: `{symbol.Name}` {symbol.Kind} {mods} | `{symbol.FullSignature}`";
    }

    /// <summary>
    /// AST 符号真实性验证结果
    /// </summary>
    public sealed record AstVerificationResult(
        List<string> FictionalReferences,
        List<string> EnhanceableReferences,
        int TotalChecked,
        int ValidCount,
        int FictionalCount)
    {
        public double AuthenticityRate => TotalChecked == 0 ? 1.0 : (double)ValidCount / TotalChecked;
    }

    /// <summary>
    /// 验证页面内容中的符号引用是否在 AST 符号列表中存在
    /// 不存在的引用标记为"疑似虚构"，未提供调用上下文的标记为"可增强"
    /// </summary>
    /// <param name="pageContent">生成的页面内容</param>
    /// <param name="astSymbols">AST 符号列表</param>
    /// <param name="astCallEdges">AST 调用边（用于判断是否有调用上下文）</param>
    public static AstVerificationResult VerifyAstAuthenticity(
        string pageContent,
        IReadOnlyList<AstSymbol> astSymbols,
        IReadOnlyList<AstCallEdge> astCallEdges)
    {
        if (astSymbols.Count == 0)
        {
            return new AstVerificationResult([], [], 0, 0, 0);
        }

        // 构建已知符号名集合
        var knownSymbolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownMethodNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var symbolsWithCallContext = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in astSymbols)
        {
            knownSymbolNames.Add(symbol.Name);
            var qualified = string.IsNullOrWhiteSpace(symbol.ParentClass)
                ? symbol.Name
                : $"{symbol.ParentClass}.{symbol.Name}";
            knownMethodNames.Add(qualified);
        }

        foreach (var edge in astCallEdges)
        {
            if (!string.IsNullOrWhiteSpace(edge.CalleeSymbol))
                symbolsWithCallContext.Add(edge.CalleeSymbol);
            if (!string.IsNullOrWhiteSpace(edge.CallerSymbol))
                symbolsWithCallContext.Add(edge.CallerSymbol);
        }

        // 从页面内容中提取代码引用（反引号包裹的标识符）
        var extractedRefs = ExtractCodeReferencesFromMarkdown(pageContent);

        var fictionalRefs = new List<string>();
        var enhanceableRefs = new List<string>();
        var totalChecked = 0;
        var validCount = 0;

        foreach (var reference in extractedRefs)
        {
            totalChecked++;

            // 检查是否为 PascalCase 类名/方法名模式
            var parts = reference.Split('.');
            var isKnown = parts.Any(p => knownSymbolNames.Contains(p)) ||
                          knownMethodNames.Contains(reference) ||
                          knownSymbolNames.Contains(reference);

            if (isKnown)
            {
                validCount++;
                // 存在但未提供调用上下文 → 标记"可增强"
                if (!symbolsWithCallContext.Contains(reference) &&
                    !parts.Any(p => symbolsWithCallContext.Contains(p)))
                {
                    enhanceableRefs.Add(reference);
                }
            }
            else
            {
                fictionalRefs.Add(reference);
            }
        }

        return new AstVerificationResult(
            fictionalRefs,
            enhanceableRefs,
            totalChecked,
            validCount,
            fictionalRefs.Count);
    }

    /// <summary>
    /// 从 Markdown 内容中提取类名/方法名引用（反引号包裹、PascalCase/camelCase 模式）
    /// </summary>
    private static List<string> ExtractCodeReferencesFromMarkdown(string content)
    {
        var refs = new List<string>();
        // 提取反引号代码引用
        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(content, @"`([A-Za-z_]\w*(?:\.\w+)*)`"))
        {
            if (match.Groups.Count > 1)
            {
                var value = match.Groups[1].Value.Trim();
                if (value.Length > 1 && !value.StartsWith("http") && !value.Contains(' '))
                {
                    refs.Add(value);
                }
            }
        }

        // 提取粗体中的类名引用 **ClassName**
        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(content, @"\*\*(\w+)\*\*"))
        {
            if (match.Groups.Count > 1)
            {
                var value = match.Groups[1].Value.Trim();
                if (value.Length > 1 && char.IsUpper(value[0]) && !value.Contains(' '))
                {
                    refs.Add(value);
                }
            }
        }

        return refs.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
    }
}
