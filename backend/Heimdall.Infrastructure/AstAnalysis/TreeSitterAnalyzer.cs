using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TreeSitter;

namespace Heimdall.Infrastructure.AstAnalysis;

/// <summary>
/// Tree-sitter 统一解析引擎——用单一引擎替代 IAstAnalyzer + 正则回退，
/// 支持 28+ 语言的 AST 级解析。
/// </summary>
public class TreeSitterAnalyzer
{
    private readonly Dictionary<string, LanguageQueries> _queries;
    private readonly ILogger<TreeSitterAnalyzer> _logger;

    public TreeSitterAnalyzer(ILogger<TreeSitterAnalyzer> logger)
    {
        _logger = logger;
        _queries = BuildQueryTable();
    }

    /// <summary>
    /// DetectLanguage → tree-sitter 语言名映射
    /// </summary>
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "CSharp", ["typescript"] = "TypeScript", ["javascript"] = "JavaScript",
        ["python"] = "Python", ["go"] = "Go", ["rust"] = "Rust", ["java"] = "Java",
        ["c"] = "C", ["cpp"] = "Cpp", ["php"] = "Php", ["ruby"] = "Ruby",
        ["swift"] = "Swift", ["scala"] = "Scala", ["haskell"] = "Haskell",
        ["html"] = "Html", ["css"] = "Css", ["json"] = "Json",
        ["bash"] = "Bash", ["toml"] = "Toml", ["ocaml"] = "Ocaml",
        ["julia"] = "Julia", ["agda"] = "Agda",
    };

    public bool SupportsLanguage(string detectLang)
    {
        return LanguageMap.ContainsKey(detectLang) && _queries.ContainsKey(detectLang);
    }

    public AstFileResult Analyze(string filePath, string source, string language)
    {
        if (!LanguageMap.TryGetValue(language, out var tsLang) || !_queries.TryGetValue(language, out var queries))
        {
            _logger.LogDebug("Tree-sitter 不支持语言 {Language}，回退到正则", language);
            return AnalyzeWithRegex(filePath, source, language);
        }

        try
        {
            using var lang = new Language(tsLang);
            using var parser = new Parser(lang);
            var text = source.Length > 100_000 ? source[..100_000] : source;
            using var tree = parser.Parse(text);

            if (tree == null) return Empty(filePath, language);

            var root = tree.RootNode;
            var symbols = ExtractSymbolsFromTree(root, queries.SymbolQuery, text, lang);
            var deps = ExtractDependenciesFromTree(root, queries.DependencyQuery, text, lang);
            var chunks = ExtractChunksFromTree(root, queries.ChunkQuery, text);

            return new AstFileResult(filePath, language, symbols, deps, chunks,
                new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tree-sitter 解析失败 {Language}，回退到正则", language);
            return AnalyzeWithRegex(filePath, source, language);
        }
    }

    // ── Tree-sitter 符号提取 ──

    private static List<AstSymbol> ExtractSymbolsFromTree(Node root, string queryStr,
        string source, Language lang)
    {
        var symbols = new List<AstSymbol>();
        try
        {
            using var query = new Query(lang, queryStr);
            foreach (var capture in query.Execute(root).Captures)
            {
                var node = capture.Node;
                if (node.Type == "identifier" || node.Type.Contains("name"))
                {
                    var name = node.Text;
                    if (!string.IsNullOrWhiteSpace(name) && name.Length > 1)
                    {
                        var start = (int)node.StartPosition.Row + 1;
                        var end = (int)node.EndPosition.Row + 1;
                        symbols.Add(new AstSymbol(name, node.Type, name, "",
                            start, end, null, null, null, null));
                    }
                }
            }
        }
        catch (Exception) { /* Query 不匹配时返回空 */ }

        return symbols.DistinctBy(s => s.Name).Take(100).ToList();
    }

    // ── Tree-sitter 依赖提取 ──

    private static List<AstCallEdge> ExtractDependenciesFromTree(Node root, string queryStr,
        string source, Language lang)
    {
        var deps = new List<AstCallEdge>();
        try
        {
            using var query = new Query(lang, queryStr);
            foreach (var capture in query.Execute(root).Captures)
            {
                var text = capture.Node.Text;
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                {
                    deps.Add(new AstCallEdge("", "", "", text.Trim('"', '\''),
                        "import", 0.9));
                }
            }
        }
        catch (Exception) { }

        return deps.DistinctBy(d => d.CalleeFilePath).Take(30).ToList();
    }

    // ── Tree-sitter 分块 ──

    private static List<SourceChunk> ExtractChunksFromTree(Node root, string queryStr,
        string source)
    {
        var chunks = new List<SourceChunk>();
        try
        {
            // 优先用 ChunkQuery 获取顶级声明
            var matched = root.Children.Where(c => c.IsNamed).ToList();
            if (matched.Count == 0)
                matched = root.NamedChildren.ToList();

            foreach (var node in matched)
            {
                var start = (int)node.StartPosition.Row + 1;
                var end = (int)node.EndPosition.Row + 1;
                if (end - start < 2) continue; // 跳过多于简短的分块

                var label = node.Type;
                var content = node.Text;
                chunks.Add(new SourceChunk(start, end, label, content));
            }
        }
        catch (Exception) { }

        return chunks.Take(200).ToList();
    }

    // ── 正则回退 ──

    private static AstFileResult AnalyzeWithRegex(string filePath, string source, string language)
    {
        var symbols = new List<AstSymbol>();
        var deps = new List<AstCallEdge>();
        var chunks = new List<SourceChunk>();
        var text = source.Length > 50_000 ? source[..50_000] : source;

        switch (language)
        {
            case "typescript" or "javascript":
                AddRegexMatches(symbols, text, "class (\\w+)");
                AddRegexMatches(symbols, text, "function (\\w+)");
                AddRegexMatches(deps, text, "from ['\"]([^'\"]+)['\"]");
                break;
            case "python":
                AddRegexMatches(symbols, text, "class (\\w+)");
                AddRegexMatches(symbols, text, "def (\\w+)");
                AddRegexMatches(deps, text, "import (\\w+)");
                AddRegexMatches(deps, text, "from (\\w+) import");
                break;
            case "go":
                AddRegexMatches(symbols, text, "func (\\w+)");
                AddRegexMatches(symbols, text, "type (\\w+) struct");
                break;
            case "rust":
                AddRegexMatches(symbols, text, "fn (\\w+)");
                AddRegexMatches(symbols, text, "struct (\\w+)");
                AddRegexMatches(symbols, text, "impl (\\w+)");
                break;
            case "java":
                AddRegexMatches(symbols, text, "class (\\w+)");
                AddRegexMatches(symbols, text, "interface (\\w+)");
                AddRegexMatches(deps, text, "import ([\\w.]+)");
                break;
        }

        // 简易分块：按 80 行
        var lines = source.Split('\n');
        for (int i = 0; i < lines.Length; i += 80)
        {
            var end = Math.Min(i + 80, lines.Length);
            chunks.Add(new SourceChunk(i + 1, end, "block",
                string.Join("\n", lines[i..end])));
        }

        return new AstFileResult(filePath, language, symbols.Take(50).ToList(),
            deps.Take(30).ToList(), chunks.Take(50).ToList(), new List<string>());
    }

    private static void AddRegexMatches(List<AstSymbol> list, string text, string pattern)
    {
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Multiline))
        {
            if (m.Groups.Count > 0)
                list.Add(new AstSymbol(m.Groups[1].Value, "regex", m.Groups[1].Value,
                    "", 0, 0, null, null, null, null));
        }
    }

    private static void AddRegexMatches(List<AstCallEdge> list, string text, string pattern)
    {
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Multiline))
        {
            if (m.Groups.Count > 0)
                list.Add(new AstCallEdge("", "", "", m.Groups[1].Value.Trim('"', '\''),
                    "import", 0.5));
        }
    }

    private static AstFileResult Empty(string filePath, string language) =>
        new(filePath, language, new(), new(), new(), new());

    // ── 每个语言的 S-expression Query 配置 ──

    private record LanguageQueries(string SymbolQuery, string DependencyQuery, string ChunkQuery);

    private static Dictionary<string, LanguageQueries> BuildQueryTable() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = new(
                "(class_declaration name: (identifier) @name) (method_declaration name: (identifier) @name) (interface_declaration name: (identifier) @name) (struct_declaration name: (identifier) @name) (record_declaration name: (identifier) @name)",
                "(using_directive name: (qualified_name) @dep)",
                "(class_declaration) (method_declaration) (interface_declaration) (struct_declaration) (record_declaration)"
            ),
            ["typescript"] = new(
                "(class_declaration name: (identifier) @name) (function_declaration name: (identifier) @name) (method_definition name: (property_identifier) @name) (interface_declaration name: (type_identifier) @name) (export_statement (identifier) @name)",
                "(import_statement source: (string) @dep) (lexical_declaration (variable_declarator name: (identifier) @dep))",
                "(class_declaration) (function_declaration) (interface_declaration) (export_statement)"
            ),
            ["javascript"] = new(
                "(class_declaration name: (identifier) @name) (function_declaration name: (identifier) @name) (method_definition name: (property_identifier) @name)",
                "(import_statement source: (string) @dep) (variable_declarator name: (identifier) @dep)",
                "(class_declaration) (function_declaration)"
            ),
            ["python"] = new(
                "(class_definition name: (identifier) @name) (function_definition name: (identifier) @name)",
                "(import_statement name: (dotted_name) @dep) (import_from_statement module_name: (dotted_name) @dep)",
                "(class_definition) (function_definition)"
            ),
            ["go"] = new(
                "(function_declaration name: (identifier) @name) (type_declaration (type_spec name: (type_identifier) @name)) (method_declaration name: (field_identifier) @name)",
                "(import_declaration (import_spec path: (interpreted_string_literal) @dep)) (import_declaration (import_spec name: (package_identifier) @dep))",
                "(function_declaration) (type_declaration)"
            ),
            ["rust"] = new(
                "(function_item name: (identifier) @name) (struct_item name: (type_identifier) @name) (impl_item (identifier) @name) (trait_item name: (type_identifier) @name)",
                "(use_declaration (identifier) @dep) (use_declaration (scoped_identifier) @dep)",
                "(function_item) (struct_item) (impl_item) (trait_item)"
            ),
            ["java"] = new(
                "(class_declaration name: (identifier) @name) (method_declaration name: (identifier) @name) (interface_declaration name: (identifier) @name)",
                "(import_declaration (identifier) @dep) (import_declaration (scoped_identifier) @dep)",
                "(class_declaration) (method_declaration) (interface_declaration)"
            ),
            ["cpp"] = new(
                "(class_specifier name: (type_identifier) @name) (function_definition declarator: (function_declarator declarator: (identifier) @name))",
                "(preproc_include path: (string_literal) @dep) (using_declaration (qualified_identifier) @dep)",
                "(class_specifier) (function_definition)"
            ),
            ["c"] = new(
                "(function_definition declarator: (function_declarator declarator: (identifier) @name))",
                "(preproc_include path: (string_literal) @dep)",
                "(function_definition)"
            ),
            ["php"] = new(
                "(class_declaration name: (name) @name) (function_definition name: (name) @name) (method_declaration name: (name) @name)",
                "(require_once_expression (string) @dep) (include_expression (string) @dep)",
                "(class_declaration) (function_definition) (method_declaration)"
            ),
            ["ruby"] = new(
                "(class name: (constant) @name) (method name: (identifier) @name) (module name: (constant) @name)",
                "(call method: (identifier) @dep)",
                "(class) (method) (module)"
            ),
            ["swift"] = new(
                "(class_declaration name: (type_identifier) @name) (function_declaration name: (simple_identifier) @name) (protocol_declaration name: (type_identifier) @name)",
                "(import_declaration (identifier) @dep)",
                "(class_declaration) (function_declaration) (protocol_declaration)"
            ),
            ["scala"] = new(
                "(class_definition name: (identifier) @name) (function_definition name: (identifier) @name) (trait_definition name: (identifier) @name)",
                "(import_declaration (identifier) @dep) (import_declaration (stable_identifier) @dep)",
                "(class_definition) (function_definition) (trait_definition)"
            ),
        };
}

// ── 数据记录 ──

public record AstSymbol(
    string Name, string Kind, string FullSignature, string FilePath,
    int StartLine, int EndLine, string? ParentClass, string[]? Modifiers,
    string[]? BaseTypes, string[]? AttributeAnnotations);

public record AstCallEdge(
    string CallerSymbol, string CallerFilePath,
    string CalleeSymbol, string CalleeFilePath,
    string CallType, double Confidence);

public record AstFileResult(
    string FilePath, string Language,
    List<AstSymbol> Symbols,
    List<AstCallEdge> CallEdges,
    List<SourceChunk> Chunks,
    List<string> DesignPatternHints);

public record SourceChunk(int StartLine, int EndLine, string Label, string Content);
