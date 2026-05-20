using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 调用图构建器——通过正则匹配从源代码提取方法级调用关系。
/// 支持 C#、TypeScript/JavaScript、Python。
/// </summary>
public sealed class CallGraphBuilder
{
    private readonly ILogger<CallGraphBuilder> _logger;

    public CallGraphBuilder(ILogger<CallGraphBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从一组源文件构建调用图。
    /// </summary>
    public CallGraph Build(IEnumerable<(string filePath, string content)> sourceFiles)
    {
        var edges = new List<CallEdge>();
        var methodDefinitions = new Dictionary<string, string>(); // symbol -> filePath

        foreach (var (filePath, content) in sourceFiles)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var methods = ext switch
            {
                ".cs" => ExtractCSharpMethods(filePath, content),
                ".ts" or ".tsx" or ".js" or ".jsx" => ExtractTypeScriptMethods(filePath, content),
                ".py" => ExtractPythonMethods(filePath, content),
                _ => Enumerable.Empty<MethodDefinition>()
            };

            foreach (var method in methods)
            {
                methodDefinitions[method.Symbol] = filePath;
            }
        }

        // 第二遍：检测调用关系
        foreach (var (filePath, content) in sourceFiles)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var callerMethods = ext switch
            {
                ".cs" => ExtractCSharpMethods(filePath, content),
                ".ts" or ".tsx" or ".js" or ".jsx" => ExtractTypeScriptMethods(filePath, content),
                ".py" => ExtractPythonMethods(filePath, content),
                _ => Enumerable.Empty<MethodDefinition>()
            };

            foreach (var caller in callerMethods)
            {
                var callees = FindCallees(caller.Body, methodDefinitions, caller.Symbol);
                foreach (var (calleeSymbol, callType) in callees)
                {
                    edges.Add(new CallEdge
                    {
                        CallerSymbol = caller.Symbol,
                        CallerFilePath = filePath,
                        CalleeSymbol = calleeSymbol,
                        CalleeFilePath = methodDefinitions.GetValueOrDefault(calleeSymbol),
                        CallType = callType,
                        Confidence = callType == "Direct" ? 0.9 : 0.6
                    });
                }
            }
        }

        _logger.LogInformation("调用图构建完成 节点={Nodes} 边={Edges}", methodDefinitions.Count, edges.Count);

        return new CallGraph
        {
            Edges = edges,
            NodeCount = methodDefinitions.Count,
            MaxDepth = CalculateMaxDepth(edges)
        };
    }

    private static IEnumerable<MethodDefinition> ExtractCSharpMethods(string filePath, string content)
    {
        // 匹配 C# 方法定义
        var pattern = @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:[\w<>\[\]?,\s]+)\s+(\w+)\s*\(([^)]*)\)\s*\{";
        var classPattern = @"(?:class|interface|struct)\s+(\w+)";

        var currentClass = "";
        var classMatch = Regex.Match(content, classPattern);
        if (classMatch.Success) currentClass = classMatch.Groups[1].Value;

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var methodName = match.Groups[1].Value;
            var symbol = string.IsNullOrEmpty(currentClass) ? methodName : $"{currentClass}.{methodName}";

            // 提取方法体（简化：到对应的闭合大括号）
            var bodyStart = match.Index + match.Length;
            var body = ExtractMethodBody(content, bodyStart);

            yield return new MethodDefinition { Symbol = symbol, FilePath = filePath, Body = body };
        }
    }

    private static IEnumerable<MethodDefinition> ExtractTypeScriptMethods(string filePath, string content)
    {
        // 匹配 TS/JS 函数和方法
        var patterns = new[]
        {
            @"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(([^)]*)\)\s*[:{]",
            @"(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>",
            @"(?:async\s+)?(\w+)\s*\([^)]*\)\s*[:{]"
        };

        var className = "";
        var classMatch = Regex.Match(content, @"class\s+(\w+)");
        if (classMatch.Success) className = classMatch.Groups[1].Value;

        var seen = new HashSet<string>();
        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(content, pattern))
            {
                var funcName = match.Groups[1].Value;
                if (IsCommonKeyword(funcName)) continue;

                var symbol = string.IsNullOrEmpty(className) ? funcName : $"{className}.{funcName}";
                if (!seen.Add(symbol)) continue;

                var bodyStart = match.Index + match.Length;
                var body = ExtractMethodBody(content, bodyStart);
                yield return new MethodDefinition { Symbol = symbol, FilePath = filePath, Body = body };
            }
        }
    }

    private static IEnumerable<MethodDefinition> ExtractPythonMethods(string filePath, string content)
    {
        var pattern = @"def\s+(\w+)\s*\(([^)]*)\)\s*(?:->[^:]*)?:";
        var classPattern = @"class\s+(\w+)";

        var className = "";
        var classMatch = Regex.Match(content, classPattern);
        if (classMatch.Success) className = classMatch.Groups[1].Value;

        foreach (Match match in Regex.Matches(content, pattern))
        {
            var funcName = match.Groups[1].Value;
            var symbol = string.IsNullOrEmpty(className) ? funcName : $"{className}.{funcName}";

            // Python 方法体提取（缩进敏感，简化为取下面 50 行）
            var lines = content[(match.Index + match.Length)..].Split('\n');
            var body = string.Join('\n', lines.Take(50));

            yield return new MethodDefinition { Symbol = symbol, FilePath = filePath, Body = body };
        }
    }

    private static List<(string symbol, string callType)> FindCallees(
        string body, Dictionary<string, string> knownMethods, string callerSymbol)
    {
        var results = new List<(string, string)>();
        if (string.IsNullOrEmpty(body)) return results;

        foreach (var (symbol, _) in knownMethods)
        {
            if (symbol == callerSymbol) continue;

            // 检查方法名是否出现在 body 中
            var shortName = symbol.Contains('.') ? symbol.Split('.').Last() : symbol;
            if (body.Contains(shortName + "(") || body.Contains(shortName + " ("))
            {
                var callType = symbol.StartsWith("I") && symbol.Contains('.') ? "Interface" : "Direct";
                results.Add((symbol, callType));
            }
        }

        return results;
    }

    private static string ExtractMethodBody(string content, int startIndex)
    {
        if (startIndex >= content.Length) return "";

        int braceCount = 1;
        int i = startIndex;
        while (i < content.Length && braceCount > 0)
        {
            if (content[i] == '{') braceCount++;
            else if (content[i] == '}') braceCount--;
            i++;
        }

        var end = Math.Min(i, content.Length);
        var body = content[startIndex..end];
        // 限制长度，避免大方法消耗过多内存
        return body.Length > 2000 ? body[..2000] : body;
    }

    private static int CalculateMaxDepth(List<CallEdge> edges)
    {
        if (edges.Count == 0) return 0;

        // 使用不回溯的 DFS 计算近似最大深度（O(V+E) per root，不会指数爆炸）
        var adjacency = edges.GroupBy(e => e.CallerSymbol)
            .ToDictionary(g => g.Key, g => g.Select(e => e.CalleeSymbol).Distinct().ToList());

        int maxDepth = 0;
        var visited = new HashSet<string>();

        void Dfs(string node, int depth)
        {
            if (depth > maxDepth) maxDepth = depth;
            if (depth > 20) return; // 防止深度过大

            if (!adjacency.TryGetValue(node, out var neighbors)) return;
            foreach (var neighbor in neighbors)
            {
                if (visited.Add(neighbor))
                {
                    Dfs(neighbor, depth + 1);
                    // 不移除 visited，避免指数级路径探索
                }
            }
        }

        foreach (var root in adjacency.Keys.Take(50))
        {
            visited.Clear();
            visited.Add(root);
            Dfs(root, 1);
        }

        return maxDepth;
    }

    private static bool IsCommonKeyword(string name)
    {
        return name is "if" or "for" or "while" or "switch" or "catch" or "return"
            or "new" or "throw" or "await" or "yield" or "get" or "set" or "constructor";
    }

    private record MethodDefinition
    {
        public string Symbol { get; init; } = "";
        public string FilePath { get; init; } = "";
        public string Body { get; init; } = "";
    }
}
