using System.ComponentModel;
using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Tools;

/// <summary>
/// 基于持久化 AST 数据的 LLM 代码 Tool 服务。
/// 所有 Tool 从 Workspace 文件或 DB 轻量索引读取，不进行实时 Tree-sitter 解析。
/// </summary>
public sealed class AstBackedCodeToolService
{
    private readonly WorkspaceService _workspace;
    private readonly ILogger<AstBackedCodeToolService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AstBackedCodeToolService(WorkspaceService workspace, ILogger<AstBackedCodeToolService> logger)
    {
        _workspace = workspace;
        _logger = logger;
    }

    // ── 4.2 ReadCodeFile ──

    [Description("从 workspace 仓库目录读取指定文件的带行号代码文本。单次最多返回 maxLines 行，超出截断并标注。")]
    public string ReadCodeFile(string astVersionId, string filePath, int maxLines = 500)
    {
        if (!Guid.TryParse(astVersionId, out var astId))
            return $"错误：无效的 AST 版本 ID: {astVersionId}";

        var repoPath = GetRepoPathForAst(astId);
        var fullPath = Path.Combine(repoPath, filePath);

        if (!File.Exists(fullPath))
            return $"错误：文件不存在 — {filePath}";

        try
        {
            var lines = File.ReadAllLines(fullPath);
            if (lines.Length <= maxLines)
            {
                return string.Join('\n', lines.Select((l, i) => $"{i + 1,6:D} | {l}"));
            }

            var truncated = lines.Take(maxLines)
                .Select((l, i) => $"{i + 1,6:D} | {l}");
            return string.Join('\n', truncated)
                + $"\n\n... 截断：共 {lines.Length} 行，仅显示前 {maxLines} 行";
        }
        catch (Exception ex)
        {
            return $"错误：读取文件失败 — {ex.Message}";
        }
    }

    // ── 4.3 SearchSymbols ──

    [Description("从 DB symbol_names_json 列搜索符号名称。返回 top-10 匹配结果（名称、类型、文件路径、行号）。")]
    public static string SearchSymbols(string symbolsJson, string query, string? symbolKind = null)
    {
        if (string.IsNullOrWhiteSpace(symbolsJson))
            return "未找到符号索引数据。";

        try
        {
            var symbols = JsonSerializer.Deserialize<List<SymbolEntry>>(symbolsJson, JsonOptions);
            if (symbols is null || symbols.Count == 0)
                return "符号索引为空。";

            var matches = symbols
                .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(symbolKind))
                matches = matches.Where(s => s.Kind.Equals(symbolKind, StringComparison.OrdinalIgnoreCase));

            var top = matches.Take(10).ToList();
            if (top.Count == 0)
                return $"未找到匹配 '{query}' 的符号。";

            return string.Join('\n', top.Select(s =>
                $"  {s.Kind,-12} {s.Name,-30} {s.File}:{s.Line}"));
        }
        catch (Exception ex)
        {
            return $"搜索符号失败：{ex.Message}";
        }
    }

    // ── 4.4 QueryCallGraph ──

    [Description("从 workspace AST 文件读取调用边数据。返回指定符号的调用者和被调用者列表。")]
    public string QueryCallGraph(string astVersionId, string symbolName, string direction = "both")
    {
        if (!Guid.TryParse(astVersionId, out var astId))
            return $"错误：无效的 AST 版本 ID: {astVersionId}";

        var astDir = _workspace.GetAstDir(astId);
        if (!Directory.Exists(astDir))
            return "错误：AST 数据目录不存在。";

        try
        {
            var callers = new List<string>();
            var callees = new List<string>();
            var filesDir = Path.Combine(astDir, "files");

            if (Directory.Exists(filesDir))
            {
                // 从 .json 文件读取解析后的结构化数据（callEdges 等）
                foreach (var jsonFile in Directory.GetFiles(filesDir, "*.json"))
                {
                    var json = File.ReadAllText(jsonFile);
                    var result = JsonSerializer.Deserialize<CstFileData>(json, JsonOptions);
                    if (result?.CallEdges is null) continue;

                    foreach (var edge in result.CallEdges)
                    {
                        if (edge.CalleeSymbol?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true)
                            callers.Add($"  ← {edge.CallerSymbol} ({edge.CallerFilePath})");
                        if (edge.CallerSymbol?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true)
                            callees.Add($"  → {edge.CalleeSymbol} ({edge.CalleeFilePath})");
                    }
                }
            }

            var parts = new List<string>();
            if (direction is "callers" or "both" && callers.Count > 0)
                parts.Add($"调用者 ({callers.Count}):\n{string.Join('\n', callers.Take(20))}");
            if (direction is "callees" or "both" && callees.Count > 0)
                parts.Add($"被调用 ({callees.Count}):\n{string.Join('\n', callees.Take(20))}");

            return parts.Count > 0
                ? string.Join("\n\n", parts)
                : $"未找到 '{symbolName}' 的调用关系。";
        }
        catch (Exception ex)
        {
            return $"查询调用图失败：{ex.Message}";
        }
    }

    // ── 4.5 RetrieveClassDefinition ──

    [Description("从 workspace ast symbols.json 中查找指定类的完整定义信息。")]
    public string RetrieveClassDefinition(string astVersionId, string className)
    {
        if (!Guid.TryParse(astVersionId, out var astId))
            return $"错误：无效的 AST 版本 ID: {astVersionId}";

        var astDir = _workspace.GetAstDir(astId);
        var symbolsPath = Path.Combine(astDir, "symbols.json");

        if (!File.Exists(symbolsPath))
            return "错误：symbols.json 不存在。";

        try
        {
            var symbols = JsonSerializer.Deserialize<List<SymbolEntry>>(
                File.ReadAllText(symbolsPath), JsonOptions);

            var matches = symbols?
                .Where(s => s.Name.Equals(className, StringComparison.OrdinalIgnoreCase))
                .Take(5).ToList();

            if (matches is null || matches.Count == 0)
                return $"未找到类 '{className}' 的定义。";

            return string.Join("\n\n", matches.Select(s =>
                $"  类: {s.Name}\n  类型: {s.Kind}\n  文件: {s.File}:{s.Line}-{s.EndLine}"));
        }
        catch (Exception ex)
        {
            return $"检索类定义失败：{ex.Message}";
        }
    }

    // ── 4.6 lookup_file ──

    [Description("从 workspace 读取指定文件的源码和符号摘要。可指定行范围。")]
    public string LookupFile(string astVersionId, string filePath, int? startLine = null, int? endLine = null)
    {
        if (!Guid.TryParse(astVersionId, out var astId))
            return $"错误：无效的 AST 版本 ID: {astVersionId}";

        var repoPath = GetRepoPathForAst(astId);
        var fullPath = Path.Combine(repoPath, filePath);

        if (!File.Exists(fullPath))
            return $"错误：文件不存在 — {filePath}";

        try
        {
            var allLines = File.ReadAllLines(fullPath);
            var from = Math.Max(1, startLine ?? 1);
            var to = Math.Min(allLines.Length, endLine ?? allLines.Length);
            var selectedLines = allLines.Skip(from - 1).Take(to - from + 1);

            var result = string.Join('\n',
                selectedLines.Select((l, i) => $"{from + i,6:D} | {l}"));

            if (to < allLines.Length)
                result += $"\n\n... 截断：共 {allLines.Length} 行，显示 {from}-{to} 行";

            // 附加符号摘要
            var astDir = _workspace.GetAstDir(astId);
            var symbolsPath = Path.Combine(astDir, "symbols.json");
            if (File.Exists(symbolsPath))
            {
                var symbols = JsonSerializer.Deserialize<List<SymbolEntry>>(
                    File.ReadAllText(symbolsPath), JsonOptions);
                var fileSymbols = symbols?
                    .Where(s => s.File.Contains(filePath, StringComparison.OrdinalIgnoreCase))
                    .Take(15).ToList();

                if (fileSymbols is { Count: > 0 })
                {
                    result += "\n\n── 文件符号 ──\n";
                    result += string.Join('\n', fileSymbols.Select(s =>
                        $"  {s.Kind,-12} {s.Name,-30} L{s.Line}"));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"错误：{ex.Message}";
        }
    }

    // ── 4.7 find_usages ──

    [Description("从 workspace AST .cst 文件中反查指定符号的所有调用位置。")]
    public string FindUsages(string astVersionId, string symbolName, string? symbolKind = null)
    {
        if (!Guid.TryParse(astVersionId, out var astId))
            return $"错误：无效的 AST 版本 ID: {astVersionId}";

        var astDir = _workspace.GetAstDir(astId);
        var filesDir = Path.Combine(astDir, "files");

        if (!Directory.Exists(filesDir))
            return "错误：AST files 目录不存在。";

        try
        {
            var usages = new List<string>();
            foreach (var jsonFile in Directory.GetFiles(filesDir, "*.json"))
            {
                var json = File.ReadAllText(jsonFile);
                var result = JsonSerializer.Deserialize<CstFileData>(json, JsonOptions);
                if (result?.CallEdges is null) continue;

                foreach (var edge in result.CallEdges)
                {
                    if (edge.CallerSymbol?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true)
                        usages.Add($"  {edge.CallerFilePath} → {edge.CalleeSymbol}");
                    if (edge.CalleeSymbol?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true)
                        usages.Add($"  {edge.CallerSymbol} → {edge.CalleeFilePath} (被调用)");
                }
            }

            if (usages.Count == 0)
                return $"未找到对 '{symbolName}' 的引用。";

            var distinct = usages.Distinct().Take(25).ToList();
            return $"'{symbolName}' 的引用 ({distinct.Count} 条):\n{string.Join('\n', distinct)}";
        }
        catch (Exception ex)
        {
            return $"查找引用失败：{ex.Message}";
        }
    }

    // ── AIFunction 工厂 ──

    /// <summary>
    /// 返回当前 AstVersion 下的所有 6 个 Tool 的 AIFunction 列表。
    /// </summary>
    public List<AIFunction> CreateAllTools(Guid astVersionId, string? symbolsJson = null)
    {
        var astIdStr = astVersionId.ToString();
        var tools = new List<AIFunction>
        {
            AIFunctionFactory.Create((string filePath, int maxLines = 500) =>
                ReadCodeFile(astIdStr, filePath, maxLines), nameof(ReadCodeFile)),
            AIFunctionFactory.Create((string query, string? symbolKind = null) =>
                SearchSymbols(symbolsJson ?? "[]", query, symbolKind), nameof(SearchSymbols)),
            AIFunctionFactory.Create((string symbolName, string direction = "both") =>
                QueryCallGraph(astIdStr, symbolName, direction), nameof(QueryCallGraph)),
            AIFunctionFactory.Create((string className) =>
                RetrieveClassDefinition(astIdStr, className), nameof(RetrieveClassDefinition)),
            AIFunctionFactory.Create((string filePath, int? startLine = null, int? endLine = null) =>
                LookupFile(astIdStr, filePath, startLine, endLine), "lookup_file"),
            AIFunctionFactory.Create((string symbolName, string? symbolKind = null) =>
                FindUsages(astIdStr, symbolName, symbolKind), "find_usages"),
        };
        return tools;
    }

    private string GetRepoPathForAst(Guid astVersionId)
    {
        // 从 AST 目录反向推导 repo 路径：ast/{id}/ → 需要知道 repo 信息
        // 简化实现：扫描 workspace/repos/ 目录，返回第一个存在的目录
        var reposDir = Path.Combine(_workspace.RootPath, "repos");
        if (Directory.Exists(reposDir))
        {
            var dirs = Directory.GetDirectories(reposDir);
            if (dirs.Length > 0)
                return dirs[0]; // 简化：返回第一个 repo
        }
        return reposDir; // fallback
    }

    // ── 数据模型 ──

    private record SymbolEntry
    {
        public string Name { get; init; } = "";
        public string Kind { get; init; } = "";
        public string File { get; init; } = "";
        public int Line { get; init; }
        public int EndLine { get; init; }
    }

    private record CstFileData
    {
        public string? FilePath { get; init; }
        public List<CallEdgeEntry>? CallEdges { get; init; }
        public List<SymbolEntry>? Symbols { get; init; }
    }

    private record CallEdgeEntry
    {
        public string? CallerSymbol { get; init; }
        public string? CallerFilePath { get; init; }
        public string? CalleeSymbol { get; init; }
        public string? CalleeFilePath { get; init; }
        public double Confidence { get; init; }
    }
}
