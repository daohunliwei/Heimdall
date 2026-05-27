using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Tasks;
using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 深度代码理解服务——编排 Tree-sitter AST 分析、依赖拓扑与 LLM 架构理解
/// </summary>
public sealed class CodeUnderstandingService : ICodeUnderstandingService
{
    private readonly DependencyTopologyService _dependencyTopology;
    private readonly TreeSitterAnalyzer _analyzer;
    private readonly TaskLlmService _llmService;
    private readonly ILogger<CodeUnderstandingService> _logger;

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go", ".rs", ".rb"
    };

    private static readonly HashSet<string> ProjectFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".sln", ".json"
    };

    public CodeUnderstandingService(
        DependencyTopologyService dependencyTopology,
        TreeSitterAnalyzer analyzer,
        TaskLlmService llmService,
        ILogger<CodeUnderstandingService> logger)
    {
        _dependencyTopology = dependencyTopology;
        _analyzer = analyzer;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<CodeUnderstandingResult> AnalyzeAsync(
        Guid repositoryVersionId,
        string repoPath,
        string provider,
        string? model,
        CancellationToken ct = default)
    {
        _logger.LogInformation("开始深度代码理解分析 RepoPath={Path}", repoPath);

        // 1. 加载源文件
        var sourceFiles = LoadSourceFiles(repoPath).ToList();
        var projectFiles = LoadProjectFiles(repoPath).ToList();

        _logger.LogInformation("加载了 {SourceCount} 个源文件和 {ProjectCount} 个项目文件",
            sourceFiles.Count, projectFiles.Count);

        // 2. 基于 AST 提取符号、调用边与模式提示
        var astResults = AnalyzeSourceFiles(sourceFiles);
        var callGraph = BuildCallGraph(astResults);

        // 3. 构建依赖拓扑（本地，无 LLM）
        _logger.LogInformation("开始构建依赖拓扑...");
        var topology = _dependencyTopology.Build(projectFiles.Concat(sourceFiles));
        _logger.LogInformation("依赖拓扑完成 模块={Modules}", topology.Modules.Count);

        // 4. 汇总设计模式（本地，无 LLM）
        _logger.LogInformation("开始检测设计模式...");
        var patterns = BuildDetectedPatterns(astResults);
        _logger.LogInformation("设计模式检测完成 模式={Patterns}", patterns.Count);

        // 5. LLM 辅助架构理解（1-2 次调用）
        _logger.LogInformation("开始 LLM 架构理解 Provider={Provider} Model={Model}...", provider, model);
        var insight = await GenerateArchitectureInsightAsync(
            callGraph, topology, patterns, provider, model, ct);
        _logger.LogInformation("LLM 架构理解完成");

        var result = new CodeUnderstandingResult
        {
            CallGraph = callGraph,
            DependencyTopology = topology,
            DesignPatterns = patterns,
            ArchitectureInsight = insight
        };

        _logger.LogInformation(
            "深度代码理解完成 调用图节点={Nodes} 边={Edges} 模块={Modules} 模式={Patterns}",
            callGraph.NodeCount, callGraph.Edges.Count,
            topology.Modules.Count, patterns.Count);

        return result;
    }

    /// <summary>
    /// 批量执行源文件 AST 分析
    /// </summary>
    private List<AstAnalysisItem> AnalyzeSourceFiles(IEnumerable<(string filePath, string content)> sourceFiles)
    {
        List<AstAnalysisItem> results = [];
        foreach (var (filePath, content) in sourceFiles)
        {
            var language = DetectLanguage(filePath);
            if (string.IsNullOrWhiteSpace(language))
            {
                continue;
            }

            var result = _analyzer.Analyze(filePath, content, language);
            results.Add(new AstAnalysisItem(filePath, language, result));
        }

        return results;
    }

    /// <summary>
    /// 将 AST 结果汇总为调用图
    /// </summary>
    private static CallGraph BuildCallGraph(IReadOnlyList<AstAnalysisItem> astResults)
    {
        var definitions = astResults
            .SelectMany(item => item.Result.Symbols)
            .Where(symbol => symbol.Kind is "method" or "function" or "constructor")
            .Select(symbol => new SymbolDefinition(
                BuildQualifiedSymbolName(symbol),
                symbol.Name,
                symbol.FilePath))
            .DistinctBy(symbol => $"{symbol.QualifiedName}|{symbol.FilePath}")
            .ToList();

        var exactMap = definitions.ToDictionary(symbol => symbol.QualifiedName, StringComparer.OrdinalIgnoreCase);
        var shortNameMap = definitions
            .GroupBy(symbol => symbol.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<CallEdge> edges = [];
        foreach (var item in astResults)
        {
            foreach (var edge in item.Result.CallEdges.Where(edge => !edge.CallType.Equals("import", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(edge.CallerSymbol) || string.IsNullOrWhiteSpace(edge.CalleeSymbol))
                {
                    continue;
                }

                exactMap.TryGetValue(edge.CalleeSymbol, out var exactTarget);
                shortNameMap.TryGetValue(edge.CalleeSymbol, out var shortTarget);
                var target = exactTarget ?? shortTarget;

                edges.Add(new CallEdge
                {
                    CallerSymbol = edge.CallerSymbol,
                    CallerFilePath = edge.CallerFilePath,
                    CalleeSymbol = target?.QualifiedName ?? edge.CalleeSymbol,
                    CalleeFilePath = target?.FilePath ?? edge.CalleeFilePath,
                    CallType = ToCallType(edge.CallType),
                    Confidence = edge.Confidence
                });
            }
        }

        var distinctEdges = edges
            .DistinctBy(edge => $"{edge.CallerSymbol}|{edge.CallerFilePath}|{edge.CalleeSymbol}|{edge.CalleeFilePath}|{edge.CallType}")
            .ToList();

        return new CallGraph
        {
            Edges = distinctEdges,
            NodeCount = definitions.Count,
            MaxDepth = CalculateMaxDepth(distinctEdges)
        };
    }

    /// <summary>
    /// 将 AST 模式提示转换为业务模型
    /// </summary>
    private static List<DetectedPattern> BuildDetectedPatterns(IReadOnlyList<AstAnalysisItem> astResults)
    {
        List<DetectedPattern> patterns = [];
        foreach (var item in astResults)
        {
            foreach (var hint in item.Result.DesignPatternHints)
            {
                var parts = hint.Split('|', 4, StringSplitOptions.TrimEntries);
                if (parts.Length < 4)
                {
                    continue;
                }

                var participantNames = ExtractParticipantNames(parts[3]);
                if (participantNames.Count == 0)
                {
                    participantNames.AddRange(item.Result.Symbols
                        .Where(symbol => symbol.Kind is "class" or "interface" or "record" or "struct")
                        .Select(symbol => symbol.Name)
                        .Take(2));
                }

                var confidence = double.TryParse(parts[1], out var parsedConfidence) ? parsedConfidence : 0.7;
                patterns.Add(new DetectedPattern
                {
                    PatternName = parts[0],
                    Confidence = confidence,
                    ModuleName = GetModuleName(parts[2]),
                    Participants = participantNames
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(name => new PatternParticipant
                        {
                            SymbolName = name,
                            Role = "Participant",
                            FilePath = parts[2]
                        })
                        .ToList()
                });
            }
        }

        return patterns
            .DistinctBy(pattern => $"{pattern.PatternName}|{pattern.ModuleName}|{string.Join(",", pattern.Participants.Select(participant => participant.SymbolName))}")
            .ToList();
    }

    /// <summary>
    /// 提取模式参与者名称
    /// </summary>
    private static List<string> ExtractParticipantNames(string detail)
    {
        return System.Text.RegularExpressions.Regex.Matches(detail, @"[A-Za-z_][A-Za-z0-9_<>]*")
            .Select(match => match.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 构造限定符号名
    /// </summary>
    private static string BuildQualifiedSymbolName(AstSymbol symbol)
    {
        return string.IsNullOrWhiteSpace(symbol.ParentClass)
            ? symbol.Name
            : $"{symbol.ParentClass}.{symbol.Name}";
    }

    /// <summary>
    /// 标准化调用类型
    /// </summary>
    private static string ToCallType(string callType)
    {
        return callType switch
        {
            "import" => "Import",
            "interface" => "Interface",
            "event" => "Event",
            _ => "Direct"
        };
    }

    /// <summary>
    /// 计算调用图最大深度
    /// </summary>
    private static int CalculateMaxDepth(IReadOnlyList<CallEdge> edges)
    {
        if (edges.Count == 0)
        {
            return 0;
        }

        var adjacency = edges
            .GroupBy(edge => edge.CallerSymbol)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.CalleeSymbol).Distinct().ToList());

        var maxDepth = 0;
        foreach (var root in adjacency.Keys.Take(100))
        {
            HashSet<string> path = [root];
            Dfs(root, 1, path);
        }

        return maxDepth;

        void Dfs(string node, int depth, HashSet<string> path)
        {
            maxDepth = Math.Max(maxDepth, depth);
            if (depth >= 30 || !adjacency.TryGetValue(node, out var neighbors))
            {
                return;
            }

            foreach (var neighbor in neighbors)
            {
                if (!path.Add(neighbor))
                {
                    continue;
                }

                Dfs(neighbor, depth + 1, path);
                path.Remove(neighbor);
            }
        }
    }

    /// <summary>
    /// 推断文件语言
    /// </summary>
    private static string DetectLanguage(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".js" => "javascript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".rb" => "ruby",
            ".php" => "php",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" or ".h" => "c",
            ".swift" => "swift",
            ".scala" => "scala",
            ".cshtml" => "razor",
            _ => string.Empty
        };
    }

    /// <summary>
    /// 提取模块名
    /// </summary>
    private static string GetModuleName(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "root";
        }

        var parts = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "root" : parts[^1];
    }

    /// <summary>
    /// AST 分析中间项
    /// </summary>
    private sealed record AstAnalysisItem(string FilePath, string Language, AstFileResult Result);

    /// <summary>
    /// 符号定义映射项
    /// </summary>
    private sealed record SymbolDefinition(string QualifiedName, string ShortName, string FilePath);

    private async Task<ArchitectureInsight> GenerateArchitectureInsightAsync(
        CallGraph callGraph, DependencyTopology topology,
        List<DetectedPattern> patterns, string provider, string? model,
        CancellationToken ct)
    {
        // 构建 LLM 分析 prompt
        var moduleList = string.Join("\n", topology.Modules.Select(m =>
            $"- {m.Name} ({m.ModuleType}, {m.FileCount} files)"));

        var depList = string.Join("\n", topology.Edges.Take(30).Select(e =>
            $"- {e.FromModule} → {e.ToModule} ({e.DependencyType})"));

        var patternList = string.Join("\n", patterns.Take(10).Select(p =>
            $"- {p.PatternName}: {string.Join(", ", p.Participants.Select(pp => pp.SymbolName))}"));

        var callGraphSummary = $"调用图：{callGraph.NodeCount} 个方法节点，{callGraph.Edges.Count} 条调用边，最大深度 {callGraph.MaxDepth}";

        var topCallers = callGraph.Edges
            .GroupBy(e => e.CallerSymbol)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => $"- {g.Key} → 调用了 {g.Count()} 个方法");

        var prompt = $"""
            请分析以下代码仓库的架构，输出 JSON 格式的架构洞察。

            ## 模块列表
            {moduleList}

            ## 依赖关系
            {depList}

            ## 调用图概况
            {callGraphSummary}
            高频调用者：
            {string.Join("\n", topCallers)}

            ## 检测到的设计模式
            {patternList}

            ## 输出要求
            请输出以下 JSON 结构（注意直接输出 JSON，不要包裹 markdown 代码块）：
            architecturePattern: 识别的架构模式名称
            patternDescription: 架构模式的详细描述
            dataFlows: 数据流列表，每项含 name/components/description
            designDecisions: 关键设计决策列表
            layers: 架构层列表，每项含 name/responsibility/keyModules
            """;

        try
        {
            var response = await _llmService.GenerateTextAsync(
                provider, model, null, prompt, ct,
                "你是一位资深软件架构师，擅长分析代码仓库的架构模式和设计决策。请用中文回答。");

            return ParseArchitectureInsight(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 架构分析失败，使用基于规则的推断");
            return InferArchitectureFromTopology(topology, patterns);
        }
    }

    private static ArchitectureInsight ParseArchitectureInsight(string llmResponse)
    {
        // 尝试从 LLM 响应中提取 JSON
        var insight = new ArchitectureInsight();

        try
        {
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = llmResponse[jsonStart..(jsonEnd + 1)];
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("architecturePattern", out var ap))
                    insight.ArchitecturePattern = ap.GetString() ?? "";
                if (root.TryGetProperty("patternDescription", out var pd))
                    insight.PatternDescription = pd.GetString() ?? "";

                if (root.TryGetProperty("designDecisions", out var dd) && dd.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    insight.DesignDecisions = dd.EnumerateArray()
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                if (root.TryGetProperty("dataFlows", out var df) && df.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var flow in df.EnumerateArray())
                    {
                        insight.DataFlows.Add(new DataFlowPath
                        {
                            Name = flow.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Description = flow.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            Components = flow.TryGetProperty("components", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Array
                                ? c.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                                : new List<string>()
                        });
                    }
                }

                if (root.TryGetProperty("layers", out var layers) && layers.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var layer in layers.EnumerateArray())
                    {
                        insight.Layers.Add(new LayerDescription
                        {
                            Name = layer.TryGetProperty("name", out var ln) ? ln.GetString() ?? "" : "",
                            Responsibility = layer.TryGetProperty("responsibility", out var r) ? r.GetString() ?? "" : "",
                            KeyModules = layer.TryGetProperty("keyModules", out var km) && km.ValueKind == System.Text.Json.JsonValueKind.Array
                                ? km.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                                : new List<string>()
                        });
                    }
                }
            }
        }
        catch
        {
            // JSON 解析失败，返回空洞察
        }

        return insight;
    }

    private static ArchitectureInsight InferArchitectureFromTopology(
        DependencyTopology topology, List<DetectedPattern> patterns)
    {
        // 基于规则的架构推断
        var hasLayers = topology.Modules.Any(m => m.Name.Contains("Api") || m.Name.Contains("Controller"))
            && topology.Modules.Any(m => m.Name.Contains("Core") || m.Name.Contains("Service"))
            && topology.Modules.Any(m => m.Name.Contains("Repository") || m.Name.Contains("Data"));

        var pattern = hasLayers ? "分层架构 (Layered Architecture)" : "模块化架构 (Modular Architecture)";

        return new ArchitectureInsight
        {
            ArchitecturePattern = pattern,
            PatternDescription = hasLayers
                ? "项目采用经典分层架构，包含 API 层、业务逻辑层和数据访问层"
                : "项目按功能模块组织代码",
            DesignDecisions = patterns.Select(p => $"使用了 {p.PatternName} 模式").Distinct().ToList(),
            Layers = topology.Modules
                .Where(m => m.ModuleType == "project")
                .Select(m => new LayerDescription
                {
                    Name = m.Name,
                    Responsibility = InferResponsibility(m.Name),
                    KeyModules = new List<string> { m.Name }
                })
                .ToList()
        };
    }

    private static string InferResponsibility(string moduleName)
    {
        var lower = moduleName.ToLowerInvariant();
        if (lower.Contains("api") || lower.Contains("web")) return "HTTP API 入口，请求路由和响应处理";
        if (lower.Contains("core") || lower.Contains("domain")) return "业务逻辑和领域模型";
        if (lower.Contains("repository") || lower.Contains("data")) return "数据持久化和查询";
        if (lower.Contains("infrastructure") || lower.Contains("infra")) return "基础设施和外部服务集成";
        if (lower.Contains("test")) return "测试代码";
        return "功能模块";
    }

    private static IEnumerable<(string filePath, string content)> LoadSourceFiles(string repoPath)
    {
        if (!Directory.Exists(repoPath)) yield break;

        var files = Directory.EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var ext = Path.GetExtension(f);
                if (!SourceExtensions.Contains(ext)) return false;
                var relative = Path.GetRelativePath(repoPath, f).Replace('\\', '/');
                return !relative.Contains("node_modules/")
                    && !relative.Contains("bin/")
                    && !relative.Contains("obj/")
                    && !relative.Contains(".git/")
                    && !relative.Contains("dist/")
                    && !relative.Contains("vendor/");
            })
            .Take(500); // 限制文件数避免内存过大

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(repoPath, file).Replace('\\', '/');
            var content = File.ReadAllText(file);
            if (content.Length > 50000) content = content[..50000]; // 限制单文件大小
            yield return (relativePath, content);
        }
    }

    private static IEnumerable<(string filePath, string content)> LoadProjectFiles(string repoPath)
    {
        if (!Directory.Exists(repoPath)) yield break;

        var patterns = new[] { "*.csproj", "*.sln", "package.json" };
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(repoPath, pattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoPath, file).Replace('\\', '/');
                if (relative.Contains("node_modules/") || relative.Contains("bin/") || relative.Contains("obj/"))
                    continue;
                yield return (relative, File.ReadAllText(file));
            }
        }
    }
}
