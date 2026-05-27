using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TreeSitter;

namespace Heimdall.Infrastructure.AstAnalysis;

/// <summary>
/// Tree-sitter 统一解析引擎
/// </summary>
public class TreeSitterAnalyzer
{
    private static readonly ConcurrentDictionary<string, Language> LanguageCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string[]> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["agda"] = ["Agda"],
        ["bash"] = ["Bash", "bash"],
        ["c"] = ["C"],
        ["cpp"] = ["C++", "cpp"],
        ["csharp"] = ["C#", "c-sharp"],
        ["css"] = ["Css", "CSS"],
        ["embedded-template"] = ["embedded-template"],
        ["go"] = ["Go"],
        ["haskell"] = ["Haskell"],
        ["html"] = ["Html", "HTML"],
        ["java"] = ["Java"],
        ["javascript"] = ["JavaScript"],
        ["jsdoc"] = ["Jsdoc", "JSDoc"],
        ["json"] = ["Json", "JSON"],
        ["julia"] = ["Julia"],
        ["ocaml"] = ["Ocaml", "OCaml"],
        ["php"] = ["Php", "PHP"],
        ["python"] = ["Python"],
        ["ql"] = ["Ql", "QL"],
        ["razor"] = ["Razor"],
        ["ruby"] = ["Ruby"],
        ["rust"] = ["Rust"],
        ["scala"] = ["Scala"],
        ["swift"] = ["Swift"],
        ["toml"] = ["Toml", "TOML"],
        ["tsq"] = ["Tsq", "TSQ"],
        ["tsx"] = ["Tsx", "TSX"],
        ["typescript"] = ["TypeScript"],
        ["verilog"] = ["Verilog"],
    };

    private static readonly HashSet<string> ModifierKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "public", "private", "protected", "internal", "static", "async", "abstract",
        "virtual", "override", "sealed", "readonly", "partial", "extern", "unsafe",
        "new", "const", "file", "required"
    };

    private static readonly HashSet<string> NameNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "identifier", "property_identifier", "field_identifier", "type_identifier",
        "simple_identifier", "qualified_name", "name", "constant", "scoped_identifier"
    };

    private static readonly HashSet<string> CallableNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "method_declaration", "function_declaration", "function_definition", "function_item",
        "method_definition", "method", "method_invocation", "constructor_declaration",
        "constructor_definition", "local_function_statement", "lambda_expression",
        "anonymous_method_expression", "arrow_function"
    };

    private static readonly HashSet<string> TypeNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "class_declaration", "interface_declaration", "struct_declaration", "record_declaration",
        "class_definition", "trait_definition", "protocol_declaration", "class_specifier",
        "struct_item", "trait_item", "module", "type_declaration"
    };

    private readonly Dictionary<string, LanguageQueries> _queries;
    private readonly ILogger<TreeSitterAnalyzer> _logger;

    /// <summary>
    /// 初始化分析器
    /// </summary>
    public TreeSitterAnalyzer(ILogger<TreeSitterAnalyzer> logger)
    {
        _logger = logger;
        _queries = BuildQueryTable();
    }

    /// <summary>
    /// 判断语言是否受支持
    /// </summary>
    public bool SupportsLanguage(string detectLang)
    {
        return LanguageMap.ContainsKey(detectLang) && _queries.ContainsKey(detectLang);
    }

    /// <summary>
    /// 创建并返回单文件 AST 分析结果
    /// </summary>
    public AstFileResult Analyze(string filePath, string source, string language)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Empty(filePath, language);
        }

        if (!LanguageMap.TryGetValue(language, out var languageIds))
        {
            _logger.LogDebug("Tree-sitter 不支持语言 {Language}，回退到正则", language);
            return AnalyzeWithRegex(filePath, source, language);
        }

        if (!_queries.TryGetValue(language, out var queries))
        {
            _logger.LogDebug("Tree-sitter 缺少语言 {Language} 的 Query 配置，回退到正则", language);
            return AnalyzeWithRegex(filePath, source, language);
        }

        try
        {
            var lang = GetOrCreateLanguage(language, languageIds);
            using var parser = new Parser(lang);
            var text = source.Length > 100_000 ? source[..100_000] : source;
            using var tree = parser.Parse(text);
            if (tree == null)
            {
                return Empty(filePath, language);
            }

            var root = tree.RootNode;
            var symbols = ExtractSymbolsFromTree(root, queries, filePath, lang);
            var imports = ExtractDependenciesFromTree(root, queries.DependencyQuery, filePath, lang);
            var callEdges = ExtractCallEdges(root, queries.CallQuery, filePath, lang, imports);
            var chunks = ExtractChunksFromTree(root, queries.ChunkQuery, lang);
            var designPatternHints = DetectDesignPatterns(root, filePath, symbols);

            var allEdges = imports
                .Concat(callEdges)
                .DistinctBy(edge => $"{edge.CallerSymbol}|{edge.CallerFilePath}|{edge.CalleeSymbol}|{edge.CalleeFilePath}|{edge.CallType}")
                .ToList();

            return new AstFileResult(filePath, language, symbols, allEdges, chunks, designPatternHints);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tree-sitter 解析失败 {Language}，回退到正则", language);
            return AnalyzeWithRegex(filePath, source, language);
        }
    }

    /// <summary>
    /// 返回指定语言的缓存实例
    /// </summary>
    public Language GetOrCreateLanguage(string language)
    {
        if (!LanguageMap.TryGetValue(language, out var languageIds))
        {
            throw new InvalidOperationException($"不支持的语言 {language}");
        }

        return GetOrCreateLanguage(language, languageIds);
    }

    /// <summary>
    /// 返回指定语言的缓存实例
    /// </summary>
    private static Language GetOrCreateLanguage(string language, IReadOnlyList<string> languageIds)
    {
        return LanguageCache.GetOrAdd(language, _ =>
        {
            List<string> triedIds = [];
            foreach (var id in languageIds)
            {
                triedIds.Add(id);
                try
                {
                    return new Language(id);
                }
                catch
                {
                    // 忽略单个 ID 失败，继续尝试下一个
                }
            }

            throw new InvalidOperationException(
                $"无法加载 Tree-sitter 语言 {language}，已尝试 ID: {string.Join(", ", triedIds)}");
        });
    }

    /// <summary>
    /// 从语法树提取完整符号信息
    /// </summary>
    private static List<AstSymbol> ExtractSymbolsFromTree(Node root, LanguageQueries queries, string filePath, Language lang)
    {
        List<AstSymbol> symbols = [];
        try
        {
            using var query = new Query(lang, queries.SymbolQuery);
            foreach (var capture in query.Execute(root).Captures)
            {
                var captureNode = capture.Node;
                var symbolNode = GetSymbolNode(captureNode);
                if (symbolNode == null)
                {
                    continue;
                }

                var nameNode = GetNameNode(symbolNode) ?? captureNode;
                var name = NormalizeNodeText(nameNode.Text);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var baseTypeNames = ExtractBaseTypeNames(symbolNode);
                var parentClass = IsTypeNode(symbolNode)
                    ? SelectPrimaryBaseType(baseTypeNames)
                    : FindEnclosingTypeName(symbolNode);

                symbols.Add(new AstSymbol(
                    name,
                    NormalizeKind(symbolNode.Type),
                    BuildFullSignature(symbolNode),
                    filePath,
                    (int)symbolNode.StartPosition.Row + 1,
                    (int)symbolNode.EndPosition.Row + 1,
                    parentClass,
                    ExtractModifiers(symbolNode),
                    FilterInterfaceLikeBaseTypes(baseTypeNames, parentClass),
                    ExtractAttributeAnnotations(symbolNode)));
            }
        }
        catch
        {
            return [];
        }

        return symbols
            .DistinctBy(symbol => $"{symbol.FilePath}|{symbol.StartLine}|{symbol.EndLine}|{symbol.Name}")
            .Take(500)
            .ToList();
    }

    /// <summary>
    /// 从语法树提取 import 与 using 依赖
    /// </summary>
    private static List<AstCallEdge> ExtractDependenciesFromTree(Node root, string queryStr, string filePath, Language lang)
    {
        List<AstCallEdge> dependencies = [];
        try
        {
            using var query = new Query(lang, queryStr);
            foreach (var capture in query.Execute(root).Captures)
            {
                var dependency = NormalizeDependency(capture.Node.Text);
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    continue;
                }

                dependencies.Add(new AstCallEdge(string.Empty, filePath, string.Empty, dependency, "import", 0.9));
            }
        }
        catch
        {
            return [];
        }

        return dependencies
            .DistinctBy(edge => edge.CalleeFilePath)
            .Take(100)
            .ToList();
    }

    /// <summary>
    /// 从语法树提取方法级调用边
    /// </summary>
    private static List<AstCallEdge> ExtractCallEdges(
        Node root,
        string queryStr,
        string filePath,
        Language lang,
        IReadOnlyList<AstCallEdge> imports)
    {
        List<AstCallEdge> callEdges = [];
        try
        {
            using var query = new Query(lang, queryStr);
            foreach (var capture in query.Execute(root).Captures)
            {
                var callerNode = FindAncestor(capture.Node, IsCallableNode);
                if (callerNode == null)
                {
                    continue;
                }

                var callerName = BuildQualifiedSymbolName(callerNode);
                var calleeName = NormalizeNodeText(capture.Node.Text);
                if (calleeName.Contains('.'))
                {
                    calleeName = calleeName.Split('.').Last();
                }
                if (string.IsNullOrWhiteSpace(callerName) || string.IsNullOrWhiteSpace(calleeName))
                {
                    continue;
                }

                var calleeFilePath = ResolveImportedTargetPath(calleeName, imports) ?? string.Empty;
                var confidence = string.IsNullOrWhiteSpace(calleeFilePath) ? 0.9 : 0.7;
                callEdges.Add(new AstCallEdge(
                    callerName,
                    filePath,
                    calleeName,
                    calleeFilePath,
                    "direct",
                    confidence));
            }
        }
        catch
        {
            return [];
        }

        return callEdges
            .DistinctBy(edge => $"{edge.CallerSymbol}|{edge.CalleeSymbol}|{edge.CalleeFilePath}")
            .Take(500)
            .ToList();
    }

    /// <summary>
    /// 从语法树提取声明级代码分块
    /// </summary>
    private static List<SourceChunk> ExtractChunksFromTree(Node root, string queryStr, Language lang)
    {
        List<SourceChunk> chunks = [];
        try
        {
            using var query = new Query(lang, queryStr);
            foreach (var capture in query.Execute(root).Captures)
            {
                var node = capture.Node;
                var start = (int)node.StartPosition.Row + 1;
                var end = (int)node.EndPosition.Row + 1;
                if (end - start < 2)
                {
                    continue;
                }

                if (node.Type.Contains("import", StringComparison.OrdinalIgnoreCase) ||
                    node.Type.Contains("using", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                chunks.Add(new SourceChunk(start, end, NormalizeKind(node.Type), node.Text));
            }
        }
        catch
        {
            return [];
        }

        return chunks
            .DistinctBy(chunk => $"{chunk.StartLine}|{chunk.EndLine}|{chunk.Label}")
            .Take(300)
            .ToList();
    }

    /// <summary>
    /// 在分析器内部检测常见设计模式
    /// </summary>
    private static List<string> DetectDesignPatterns(Node root, string filePath, IReadOnlyList<AstSymbol> symbols)
    {
        List<string> hints = [];
        var nodes = EnumerateDescendants(root).ToList();
        if (HasFactoryPattern(nodes, symbols, out var factoryHint))
        {
            hints.Add(BuildPatternHint("Factory", 0.90, filePath, factoryHint));
        }

        if (HasStrategyPattern(symbols, out var strategyHint))
        {
            hints.Add(BuildPatternHint("Strategy", 0.85, filePath, strategyHint));
        }

        if (HasObserverPattern(nodes, symbols, out var observerHint))
        {
            hints.Add(BuildPatternHint("Observer", 0.80, filePath, observerHint));
        }

        if (HasSingletonPattern(nodes, symbols, out var singletonHint))
        {
            hints.Add(BuildPatternHint("Singleton", 0.90, filePath, singletonHint));
        }

        if (HasBuilderPattern(nodes, symbols, out var builderHint))
        {
            hints.Add(BuildPatternHint("Builder", 0.85, filePath, builderHint));
        }

        if (HasRepositoryPattern(symbols, out var repositoryHint))
        {
            hints.Add(BuildPatternHint("Repository", 0.95, filePath, repositoryHint));
        }

        if (HasMediatorPattern(nodes, symbols, out var mediatorHint))
        {
            hints.Add(BuildPatternHint("Mediator", 0.80, filePath, mediatorHint));
        }

        return hints;
    }

    /// <summary>
    /// 检测工厂模式
    /// </summary>
    private static bool HasFactoryPattern(IReadOnlyList<Node> nodes, IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var creator = symbols.FirstOrDefault(symbol =>
            symbol.Kind == "class" &&
            symbol.Name.Contains("Factory", StringComparison.OrdinalIgnoreCase));

        if (creator == null)
        {
            return false;
        }

        if (nodes.Any(node => node.Type == "object_creation_expression"))
        {
            detail = creator.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检测策略模式
    /// </summary>
    private static bool HasStrategyPattern(IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var strategyInterface = symbols.FirstOrDefault(symbol =>
            symbol.Kind == "interface" &&
            (symbol.Name.Contains("Strategy", StringComparison.OrdinalIgnoreCase) ||
             symbol.Name.Contains("Policy", StringComparison.OrdinalIgnoreCase) ||
             symbol.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
             symbol.Name.Contains("Processor", StringComparison.OrdinalIgnoreCase)));

        if (strategyInterface == null)
        {
            return false;
        }

        var implementations = symbols
            .Where(symbol => symbol.Kind == "class" &&
                symbol.BaseTypes?.Any(baseType => baseType.Equals(strategyInterface.Name, StringComparison.OrdinalIgnoreCase)) == true)
            .Select(symbol => symbol.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (implementations.Count >= 2)
        {
            detail = $"{strategyInterface.Name}:{string.Join(",", implementations)}";
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检测观察者模式
    /// </summary>
    private static bool HasObserverPattern(IReadOnlyList<Node> nodes, IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var subject = symbols.FirstOrDefault(symbol => symbol.Kind == "class");
        if (subject == null)
        {
            return false;
        }

        if (nodes.Any(node => node.Type.Contains("event", StringComparison.OrdinalIgnoreCase)))
        {
            detail = subject.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检测单例模式
    /// </summary>
    private static bool HasSingletonPattern(IReadOnlyList<Node> nodes, IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var singletonClass = symbols.FirstOrDefault(symbol => symbol.Kind == "class");
        if (singletonClass == null)
        {
            return false;
        }

        var hasPrivateCtor = nodes.Any(node =>
            node.Type.Contains("constructor", StringComparison.OrdinalIgnoreCase) &&
            NormalizeWhitespace(node.Text).Contains($"private {singletonClass.Name}", StringComparison.OrdinalIgnoreCase));

        var hasStaticInstance = nodes.Any(node =>
            node.Type.Contains("field", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Contains("property", StringComparison.OrdinalIgnoreCase)
                ? NormalizeWhitespace(node.Text).Contains(singletonClass.Name, StringComparison.OrdinalIgnoreCase) &&
                  NormalizeWhitespace(node.Text).Contains("static", StringComparison.OrdinalIgnoreCase) &&
                  NormalizeWhitespace(node.Text).Contains("Instance", StringComparison.OrdinalIgnoreCase)
                : false);

        if (hasPrivateCtor && hasStaticInstance)
        {
            detail = singletonClass.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检测建造者模式
    /// </summary>
    private static bool HasBuilderPattern(IReadOnlyList<Node> nodes, IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var builder = symbols.FirstOrDefault(symbol =>
            symbol.Kind == "class" &&
            symbol.Name.Contains("Builder", StringComparison.OrdinalIgnoreCase));

        if (builder == null)
        {
            return false;
        }

        if (nodes.Any(node =>
                node.Type.Contains("method", StringComparison.OrdinalIgnoreCase) &&
                NormalizeWhitespace(node.Text).Contains("Build(", StringComparison.OrdinalIgnoreCase)))
        {
            detail = builder.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检测仓储模式
    /// </summary>
    private static bool HasRepositoryPattern(IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        var repository = symbols
            .Where(symbol => symbol.Kind is "class" or "interface" or "record" or "struct")
            .Select(symbol => symbol.Name)
            .FirstOrDefault(name => name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        detail = repository ?? string.Empty;
        return !string.IsNullOrWhiteSpace(repository);
    }

    /// <summary>
    /// 检测中介者模式
    /// </summary>
    private static bool HasMediatorPattern(IReadOnlyList<Node> nodes, IReadOnlyList<AstSymbol> symbols, out string detail)
    {
        detail = string.Empty;
        var mediator = symbols.FirstOrDefault(symbol =>
            symbol.Kind == "class" &&
            (symbol.Name.Contains("Mediator", StringComparison.OrdinalIgnoreCase) ||
             symbol.Name.Contains("Orchestrator", StringComparison.OrdinalIgnoreCase) ||
             symbol.Name.Contains("Coordinator", StringComparison.OrdinalIgnoreCase)));

        if (mediator == null)
        {
            return false;
        }

        var fieldCount = nodes.Count(node => node.Type.Contains("field", StringComparison.OrdinalIgnoreCase));
        if (fieldCount >= 3)
        {
            detail = mediator.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 返回格式化后的模式提示
    /// </summary>
    private static string BuildPatternHint(string name, double confidence, string filePath, string detail)
    {
        return $"{name}|{confidence:F2}|{filePath}|{detail}";
    }

    /// <summary>
    /// 获取用于构造符号的声明节点
    /// </summary>
    private static Node? GetSymbolNode(Node captureNode)
    {
        var parent = captureNode.Parent;
        if (parent == null)
        {
            return captureNode;
        }

        var parentNameNode = GetNameNode(parent);
        if (parentNameNode != null && parentNameNode.Id == captureNode.Id)
        {
            return parent;
        }

        if (NameNodeTypes.Contains(captureNode.Type))
        {
            return parent;
        }

        return captureNode;
    }

    /// <summary>
    /// 获取声明节点的名称字段
    /// </summary>
    private static Node? GetNameNode(Node node)
    {
        foreach (var fieldName in new[] { "name", "declarator" })
        {
            var fieldNode = node.GetChildForField(fieldName);
            if (fieldNode == null)
            {
                continue;
            }

            if (fieldName == "declarator")
            {
                return FindFirstNameLikeDescendant(fieldNode) ?? fieldNode;
            }

            return fieldNode;
        }

        return FindFirstNameLikeDescendant(node);
    }

    /// <summary>
    /// 查找首个名称类后代节点
    /// </summary>
    private static Node? FindFirstNameLikeDescendant(Node node)
    {
        foreach (var descendant in EnumerateDescendants(node))
        {
            if (NameNodeTypes.Contains(descendant.Type))
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据节点类型标准化 Kind
    /// </summary>
    private static string NormalizeKind(string nodeType)
    {
        return nodeType switch
        {
            var type when type.Contains("class", StringComparison.OrdinalIgnoreCase) => "class",
            var type when type.Contains("interface", StringComparison.OrdinalIgnoreCase) => "interface",
            var type when type.Contains("struct", StringComparison.OrdinalIgnoreCase) => "struct",
            var type when type.Contains("record", StringComparison.OrdinalIgnoreCase) => "record",
            var type when type.Contains("method", StringComparison.OrdinalIgnoreCase) => "method",
            var type when type.Contains("function", StringComparison.OrdinalIgnoreCase) => "function",
            var type when type.Contains("constructor", StringComparison.OrdinalIgnoreCase) => "constructor",
            var type when type.Contains("property", StringComparison.OrdinalIgnoreCase) => "property",
            var type when type.Contains("field", StringComparison.OrdinalIgnoreCase) => "field",
            var type when type.Contains("protocol", StringComparison.OrdinalIgnoreCase) => "protocol",
            var type when type.Contains("trait", StringComparison.OrdinalIgnoreCase) => "trait",
            _ => nodeType.Replace("_declaration", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_definition", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_expression", string.Empty, StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// 构造完整签名字符串
    /// </summary>
    private static string BuildFullSignature(Node node)
    {
        var text = NormalizeWhitespace(node.Text);
        foreach (var delimiter in new[] { "{", "=>", ";" })
        {
            var index = text.IndexOf(delimiter, StringComparison.Ordinal);
            if (index > 0)
            {
                text = text[..index].Trim();
                break;
            }
        }

        return text;
    }

    /// <summary>
    /// 提取修饰符列表
    /// </summary>
    private static string[]? ExtractModifiers(Node node)
    {
        List<string> modifiers = [];
        var modifiersNode = node.GetChildForField("modifiers");
        if (modifiersNode != null)
        {
            foreach (var descendant in EnumerateDescendants(modifiersNode))
            {
                var text = NormalizeNodeText(descendant.Text);
                if (ModifierKeywords.Contains(text))
                {
                    modifiers.Add(text);
                }
            }
        }

        if (modifiers.Count == 0)
        {
            foreach (var child in node.Children)
            {
                var text = NormalizeNodeText(child.Text);
                if (ModifierKeywords.Contains(text))
                {
                    modifiers.Add(text);
                }
            }
        }

        return modifiers.Count == 0
            ? null
            : modifiers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 提取基类与接口名称列表
    /// </summary>
    private static List<string> ExtractBaseTypeNames(Node node)
    {
        List<string> baseTypes = [];
        foreach (var fieldName in new[] { "bases", "base", "interfaces", "superclass" })
        {
            var fieldNode = node.GetChildForField(fieldName);
            if (fieldNode != null)
            {
                baseTypes.AddRange(ExtractTypeNamesFromNode(fieldNode));
            }
        }

        foreach (var descendant in node.NamedChildren.Where(child =>
                     child.Type.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                     child.Type.Contains("extends", StringComparison.OrdinalIgnoreCase) ||
                     child.Type.Contains("implements", StringComparison.OrdinalIgnoreCase) ||
                     child.Type.Contains("inherit", StringComparison.OrdinalIgnoreCase)))
        {
            baseTypes.AddRange(ExtractTypeNamesFromNode(descendant));
        }

        return baseTypes
            .Select(NormalizeNodeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 从节点中提取类型名称
    /// </summary>
    private static IEnumerable<string> ExtractTypeNamesFromNode(Node node)
    {
        foreach (var descendant in EnumerateDescendants(node))
        {
            if (NameNodeTypes.Contains(descendant.Type) ||
                descendant.Type.Contains("type", StringComparison.OrdinalIgnoreCase))
            {
                var value = NormalizeNodeText(descendant.Text);
                if (!string.IsNullOrWhiteSpace(value) &&
                    !ModifierKeywords.Contains(value) &&
                    value != ":")
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>
    /// 返回最可能的父类类型
    /// </summary>
    private static string? SelectPrimaryBaseType(IReadOnlyList<string> baseTypes)
    {
        if (baseTypes.Count == 0)
        {
            return null;
        }

        return baseTypes.FirstOrDefault(type =>
            !type.StartsWith('I') || type.Length < 2 || !char.IsUpper(type[1]));
    }

    /// <summary>
    /// 过滤接口类基类型
    /// </summary>
    private static string[]? FilterInterfaceLikeBaseTypes(IReadOnlyList<string> baseTypes, string? parentClass)
    {
        var items = baseTypes
            .Where(type => !type.Equals(parentClass, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return items.Length == 0 ? null : items;
    }

    /// <summary>
    /// 提取属性注解
    /// </summary>
    private static string[]? ExtractAttributeAnnotations(Node node)
    {
        var annotations = EnumerateDescendants(node)
            .Where(descendant => descendant.Type.Contains("attribute", StringComparison.OrdinalIgnoreCase))
            .Select(descendant => NormalizeWhitespace(descendant.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return annotations.Length == 0 ? null : annotations;
    }

    /// <summary>
    /// 查找所在类型名称
    /// </summary>
    private static string? FindEnclosingTypeName(Node node)
    {
        var typeNode = FindAncestor(node.Parent, IsTypeNode);
        var nameNode = typeNode == null ? null : GetNameNode(typeNode);
        return nameNode == null ? null : NormalizeNodeText(nameNode.Text);
    }

    /// <summary>
    /// 构造调用者限定名
    /// </summary>
    private static string BuildQualifiedSymbolName(Node callableNode)
    {
        var callableNameNode = GetNameNode(callableNode);
        var callableName = callableNameNode == null ? string.Empty : NormalizeNodeText(callableNameNode.Text);
        if (string.IsNullOrWhiteSpace(callableName))
        {
            return string.Empty;
        }

        var parentType = FindEnclosingTypeName(callableNode);
        return string.IsNullOrWhiteSpace(parentType)
            ? callableName
            : $"{parentType}.{callableName}";
    }

    /// <summary>
    /// 从 import 依赖推定目标路径
    /// </summary>
    private static string? ResolveImportedTargetPath(string calleeName, IReadOnlyList<AstCallEdge> imports)
    {
        foreach (var importEdge in imports)
        {
            var importPath = NormalizeDependency(importEdge.CalleeFilePath);
            if (string.IsNullOrWhiteSpace(importPath))
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(importPath);
            if (fileName.Equals(calleeName, StringComparison.OrdinalIgnoreCase) ||
                importPath.Contains(calleeName, StringComparison.OrdinalIgnoreCase))
            {
                return importPath;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断节点是否为类型声明
    /// </summary>
    private static bool IsTypeNode(Node node)
    {
        return TypeNodeTypes.Contains(node.Type) || node.Type.Contains("class", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断节点是否为可调用声明
    /// </summary>
    private static bool IsCallableNode(Node node)
    {
        return CallableNodeTypes.Contains(node.Type) ||
               node.Type.Contains("method", StringComparison.OrdinalIgnoreCase) ||
               node.Type.Contains("function", StringComparison.OrdinalIgnoreCase) ||
               node.Type.Contains("constructor", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 向上查找满足条件的祖先节点
    /// </summary>
    private static Node? FindAncestor(Node? node, Func<Node, bool> predicate)
    {
        var current = node;
        while (current != null)
        {
            if (predicate(current))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// 枚举当前节点的所有后代
    /// </summary>
    private static IEnumerable<Node> EnumerateDescendants(Node node)
    {
        Queue<Node> queue = new();
        queue.Enqueue(node);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var child in current.NamedChildren)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// 标准化节点文本
    /// </summary>
    private static string NormalizeNodeText(string text)
    {
        return NormalizeWhitespace(text).Trim('"', '\'', '`');
    }

    /// <summary>
    /// 标准化空白字符
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    /// <summary>
    /// 标准化依赖路径文本
    /// </summary>
    private static string NormalizeDependency(string text)
    {
        return NormalizeNodeText(text)
            .TrimStart('@')
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// 正则回退分析
    /// </summary>
    private static AstFileResult AnalyzeWithRegex(string filePath, string source, string language)
    {
        List<AstSymbol> symbols = [];
        List<AstCallEdge> deps = [];
        List<SourceChunk> chunks = [];
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

        var lines = source.Split('\n');
        for (int i = 0; i < lines.Length; i += 80)
        {
            var end = Math.Min(i + 80, lines.Length);
            chunks.Add(new SourceChunk(i + 1, end, "block", string.Join("\n", lines[i..end])));
        }

        return new AstFileResult(
            filePath,
            language,
            symbols.Take(50).ToList(),
            deps.Take(30).ToList(),
            chunks.Take(50).ToList(),
            []);
    }

    /// <summary>
    /// 向符号列表追加正则匹配结果
    /// </summary>
    private static void AddRegexMatches(List<AstSymbol> list, string text, string pattern)
    {
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Multiline))
        {
            if (match.Groups.Count <= 1)
            {
                continue;
            }

            list.Add(new AstSymbol(
                match.Groups[1].Value,
                "regex",
                match.Groups[1].Value,
                string.Empty,
                0,
                0,
                null,
                null,
                null,
                null));
        }
    }

    /// <summary>
    /// 向依赖列表追加正则匹配结果
    /// </summary>
    private static void AddRegexMatches(List<AstCallEdge> list, string text, string pattern)
    {
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.Multiline))
        {
            if (match.Groups.Count <= 1)
            {
                continue;
            }

            list.Add(new AstCallEdge(
                string.Empty,
                string.Empty,
                string.Empty,
                match.Groups[1].Value.Trim('"', '\''),
                "import",
                0.5));
        }
    }

    /// <summary>
    /// 返回空分析结果
    /// </summary>
    private static AstFileResult Empty(string filePath, string language)
    {
        return new(filePath, language, [], [], [], []);
    }

    /// <summary>
    /// 构建各语言 Query 表
    /// </summary>
    private static Dictionary<string, LanguageQueries> BuildQueryTable()
    {
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = new(
                "(class_declaration name: (identifier) @name) (method_declaration name: (identifier) @name) (interface_declaration name: (identifier) @name) (struct_declaration name: (identifier) @name) (record_declaration name: (identifier) @name) (property_declaration name: (identifier) @name) (constructor_declaration name: (identifier) @name)",
                "(using_directive name: (qualified_name) @dep)",
                "(class_declaration) @chunk (method_declaration) @chunk (interface_declaration) @chunk (struct_declaration) @chunk (record_declaration) @chunk (property_declaration) @chunk",
                "(invocation_expression function: [(member_access_expression name: (identifier) @callee) (identifier) @callee])"
            ),
            ["typescript"] = new(
                "(class_declaration name: (identifier) @name) (function_declaration name: (identifier) @name) (method_definition name: (property_identifier) @name) (interface_declaration name: (type_identifier) @name)",
                "(import_statement source: (string) @dep)",
                "(class_declaration) @chunk (function_declaration) @chunk (interface_declaration) @chunk (method_definition) @chunk",
                "(call_expression function: [(identifier) @callee (member_expression property: (property_identifier) @callee)])"
            ),
            ["javascript"] = new(
                "(class_declaration name: (identifier) @name) (function_declaration name: (identifier) @name) (method_definition name: (property_identifier) @name)",
                "(import_statement source: (string) @dep)",
                "(class_declaration) @chunk (function_declaration) @chunk (method_definition) @chunk",
                "(call_expression function: [(identifier) @callee (member_expression property: (property_identifier) @callee)])"
            ),
            ["python"] = new(
                "(class_definition name: (identifier) @name) (function_definition name: (identifier) @name)",
                "(import_statement name: (dotted_name) @dep) (import_from_statement module_name: (dotted_name) @dep)",
                "(class_definition) @chunk (function_definition) @chunk",
                "(call function: [(identifier) @callee (attribute attribute: (identifier) @callee)])"
            ),
            ["go"] = new(
                "(function_declaration name: (identifier) @name) (type_declaration (type_spec name: (type_identifier) @name)) (method_declaration name: (field_identifier) @name)",
                "(import_declaration (import_spec path: (interpreted_string_literal) @dep))",
                "(function_declaration) @chunk (type_declaration) @chunk (method_declaration) @chunk",
                "(call_expression function: [(identifier) @callee (selector_expression field: (field_identifier) @callee)])"
            ),
            ["rust"] = new(
                "(function_item name: (identifier) @name) (struct_item name: (type_identifier) @name) (trait_item name: (type_identifier) @name)",
                "(use_declaration [(identifier) (scoped_identifier)] @dep)",
                "(function_item) @chunk (struct_item) @chunk (trait_item) @chunk",
                "(call_expression function: [(identifier) @callee (field_expression field: (field_identifier) @callee)])"
            ),
            ["java"] = new(
                "(class_declaration name: (identifier) @name) (method_declaration name: (identifier) @name) (interface_declaration name: (identifier) @name)",
                "(import_declaration [(identifier) (scoped_identifier)] @dep)",
                "(class_declaration) @chunk (method_declaration) @chunk (interface_declaration) @chunk",
                "(method_invocation name: (identifier) @callee)"
            ),
            ["cpp"] = new(
                "(class_specifier name: (type_identifier) @name) (function_definition declarator: (function_declarator declarator: (identifier) @name))",
                "(preproc_include path: (string_literal) @dep)",
                "(class_specifier) @chunk (function_definition) @chunk",
                "(call_expression function: [(identifier) @callee (field_expression field: (field_identifier) @callee)])"
            ),
            ["c"] = new(
                "(function_definition declarator: (function_declarator declarator: (identifier) @name))",
                "(preproc_include path: (string_literal) @dep)",
                "(function_definition) @chunk",
                "(call_expression function: (identifier) @callee)"
            ),
            ["php"] = new(
                "(class_declaration name: (name) @name) (function_definition name: (name) @name) (method_declaration name: (name) @name)",
                "(require_once_expression (string) @dep) (include_expression (string) @dep)",
                "(class_declaration) @chunk (function_definition) @chunk (method_declaration) @chunk",
                "(function_call_expression function: [(name) @callee (qualified_name) @callee])"
            ),
            ["ruby"] = new(
                "(class name: (constant) @name) (method name: (identifier) @name) (module name: (constant) @name)",
                "(call method: (identifier) @dep)",
                "(class) @chunk (method) @chunk (module) @chunk",
                "(call method: (identifier) @callee)"
            ),
            ["swift"] = new(
                "(class_declaration name: (type_identifier) @name) (function_declaration name: (simple_identifier) @name) (protocol_declaration name: (type_identifier) @name)",
                "(import_declaration (identifier) @dep)",
                "(class_declaration) @chunk (function_declaration) @chunk (protocol_declaration) @chunk",
                "(call_expression called_expression: [(simple_identifier) @callee (navigation_suffix name: (simple_identifier) @callee)])"
            ),
            ["scala"] = new(
                "(class_definition name: (identifier) @name) (function_definition name: (identifier) @name) (trait_definition name: (identifier) @name)",
                "(import_declaration (identifier) @dep) (import_declaration (stable_identifier) @dep)",
                "(class_definition) @chunk (function_definition) @chunk (trait_definition) @chunk",
                "(call_expression function: (identifier) @callee)"
            ),
        };
    }

    /// <summary>
    /// 语言 Query 配置
    /// </summary>
    private sealed record LanguageQueries(string SymbolQuery, string DependencyQuery, string ChunkQuery, string CallQuery);
}

/// <summary>
/// AST 符号记录
/// </summary>
public record AstSymbol(
    string Name,
    string Kind,
    string FullSignature,
    string FilePath,
    int StartLine,
    int EndLine,
    string? ParentClass,
    string[]? Modifiers,
    string[]? BaseTypes,
    string[]? AttributeAnnotations);

/// <summary>
/// AST 调用边记录
/// </summary>
public record AstCallEdge(
    string CallerSymbol,
    string CallerFilePath,
    string CalleeSymbol,
    string CalleeFilePath,
    string CallType,
    double Confidence);

/// <summary>
/// AST 单文件分析结果
/// </summary>
public record AstFileResult(
    string FilePath,
    string Language,
    List<AstSymbol> Symbols,
    List<AstCallEdge> CallEdges,
    List<SourceChunk> Chunks,
    List<string> DesignPatternHints);

/// <summary>
/// 源代码分块记录
/// </summary>
public record SourceChunk(int StartLine, int EndLine, string Label, string Content);
