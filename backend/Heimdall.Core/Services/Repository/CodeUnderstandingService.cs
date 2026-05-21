using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Tasks;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 深度代码理解服务——编排 CallGraphBuilder + DependencyTopologyService + DesignPatternDetector + LLM 架构理解。
/// </summary>
public sealed class CodeUnderstandingService : ICodeUnderstandingService
{
    private readonly CallGraphBuilder _callGraphBuilder;
    private readonly DependencyTopologyService _dependencyTopology;
    private readonly DesignPatternDetector _patternDetector;
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
        CallGraphBuilder callGraphBuilder,
        DependencyTopologyService dependencyTopology,
        DesignPatternDetector patternDetector,
        TaskLlmService llmService,
        ILogger<CodeUnderstandingService> logger)
    {
        _callGraphBuilder = callGraphBuilder;
        _dependencyTopology = dependencyTopology;
        _patternDetector = patternDetector;
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

        // 2. 构建调用图（本地，无 LLM）
        var callGraph = _callGraphBuilder.Build(sourceFiles);

        // 3. 构建依赖拓扑（本地，无 LLM）
        _logger.LogInformation("开始构建依赖拓扑...");
        var topology = _dependencyTopology.Build(projectFiles.Concat(sourceFiles));
        _logger.LogInformation("依赖拓扑完成 模块={Modules}", topology.Modules.Count);

        // 4. 检测设计模式（本地，无 LLM）
        _logger.LogInformation("开始检测设计模式...");
        var patterns = _patternDetector.Detect(sourceFiles);
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
