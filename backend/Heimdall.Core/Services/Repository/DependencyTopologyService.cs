using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 模块依赖拓扑服务——解析项目文件和 import 语句，构建模块间有向图。
/// </summary>
public sealed class DependencyTopologyService
{
    private readonly ILogger<DependencyTopologyService> _logger;

    public DependencyTopologyService(ILogger<DependencyTopologyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从项目文件和源码构建模块依赖拓扑。
    /// </summary>
    public DependencyTopology Build(IEnumerable<(string filePath, string content)> projectFiles)
    {
        var modules = new Dictionary<string, ModuleNode>();
        var edges = new List<DependencyEdge>();

        foreach (var (filePath, content) in projectFiles)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".csproj":
                    ParseCsProj(filePath, content, modules, edges);
                    break;
                case ".sln":
                    ParseSolution(filePath, content, modules);
                    break;
                case ".json" when filePath.EndsWith("package.json"):
                    ParsePackageJson(filePath, content, modules, edges);
                    break;
            }
        }

        // 如果没有项目文件，按目录结构推断模块
        if (modules.Count == 0)
        {
            InferModulesFromDirectories(projectFiles, modules, edges);
        }

        // 检测循环依赖
        var cyclic = DetectCycles(modules.Keys.ToList(), edges);

        _logger.LogInformation("依赖拓扑构建完成 模块={Modules} 边={Edges} 循环={Cycles}",
            modules.Count, edges.Count, cyclic.Count);

        return new DependencyTopology
        {
            Modules = modules.Values.ToList(),
            Edges = edges,
            CyclicPaths = cyclic
        };
    }

    private static void ParseCsProj(string filePath, string content,
        Dictionary<string, ModuleNode> modules, List<DependencyEdge> edges)
    {
        var moduleName = Path.GetFileNameWithoutExtension(filePath);
        modules.TryAdd(moduleName, new ModuleNode
        {
            Name = moduleName,
            ModuleType = "project"
        });

        // ProjectReference
        foreach (Match match in Regex.Matches(content, @"<ProjectReference\s+Include=""([^""]+)"""))
        {
            var refPath = match.Groups[1].Value;
            var refName = Path.GetFileNameWithoutExtension(refPath);
            modules.TryAdd(refName, new ModuleNode { Name = refName, ModuleType = "project" });
            edges.Add(new DependencyEdge
            {
                FromModule = moduleName,
                ToModule = refName,
                DependencyType = "Compile"
            });
        }

        // PackageReference（记录为外部依赖）
        foreach (Match match in Regex.Matches(content, @"<PackageReference\s+Include=""([^""]+)"""))
        {
            var pkgName = match.Groups[1].Value;
            modules.TryAdd(pkgName, new ModuleNode { Name = pkgName, ModuleType = "package" });
            edges.Add(new DependencyEdge
            {
                FromModule = moduleName,
                ToModule = pkgName,
                DependencyType = "Compile"
            });
        }
    }

    private static void ParseSolution(string filePath, string content, Dictionary<string, ModuleNode> modules)
    {
        foreach (Match match in Regex.Matches(content, @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)"""))
        {
            var projName = match.Groups[1].Value;
            modules.TryAdd(projName, new ModuleNode { Name = projName, ModuleType = "project" });
        }
    }

    private static void ParsePackageJson(string filePath, string content,
        Dictionary<string, ModuleNode> modules, List<DependencyEdge> edges)
    {
        var moduleName = GetModuleNameFromPath(filePath);
        modules.TryAdd(moduleName, new ModuleNode { Name = moduleName, ModuleType = "package" });

        // 简化解析 dependencies
        var depPatterns = new[] { @"""dependencies""\s*:\s*\{([^}]*)\}", @"""devDependencies""\s*:\s*\{([^}]*)\}" };
        foreach (var pattern in depPatterns)
        {
            var match = Regex.Match(content, pattern);
            if (!match.Success) continue;

            foreach (Match dep in Regex.Matches(match.Groups[1].Value, @"""([^""]+)""\s*:"))
            {
                var depName = dep.Groups[1].Value;
                if (depName.StartsWith("@types/")) continue; // 忽略类型定义
                modules.TryAdd(depName, new ModuleNode { Name = depName, ModuleType = "package" });
                edges.Add(new DependencyEdge
                {
                    FromModule = moduleName,
                    ToModule = depName,
                    DependencyType = pattern.Contains("dev") ? "Test" : "Runtime"
                });
            }
        }
    }

    private static void InferModulesFromDirectories(
        IEnumerable<(string filePath, string content)> files,
        Dictionary<string, ModuleNode> modules,
        List<DependencyEdge> edges)
    {
        // 按顶层目录分组为模块
        var dirGroups = files
            .Select(f => f.filePath)
            .Where(f => f.Contains('/') || f.Contains('\\'))
            .GroupBy(f =>
            {
                var parts = f.Replace('\\', '/').Split('/');
                return parts.Length > 1 ? parts[0] : "root";
            })
            .Where(g => g.Count() >= 2);

        foreach (var group in dirGroups)
        {
            modules.TryAdd(group.Key, new ModuleNode
            {
                Name = group.Key,
                ModuleType = "directory",
                FileCount = group.Count()
            });
        }

        // import 语句检测跨目录依赖
        foreach (var (filePath, content) in files)
        {
            var currentModule = GetModuleFromPath(filePath, modules.Keys);
            if (currentModule == null) continue;

            foreach (Match match in Regex.Matches(content, @"(?:import|from|require)\s*[('""]+([^'"")]+)"))
            {
                var importPath = match.Groups[1].Value;
                if (importPath.StartsWith(".")) continue; // 相对路径内部引用

                var targetModule = modules.Keys.FirstOrDefault(m =>
                    importPath.Contains(m, StringComparison.OrdinalIgnoreCase));
                if (targetModule != null && targetModule != currentModule)
                {
                    edges.Add(new DependencyEdge
                    {
                        FromModule = currentModule,
                        ToModule = targetModule,
                        DependencyType = "Compile"
                    });
                }
            }
        }
    }

    private static List<List<string>> DetectCycles(List<string> nodes, List<DependencyEdge> edges)
    {
        var cycles = new List<List<string>>();
        var adjacency = edges
            .Where(e => nodes.Contains(e.FromModule) && nodes.Contains(e.ToModule))
            .GroupBy(e => e.FromModule)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToModule).Distinct().ToList());

        var visited = new HashSet<string>();
        var path = new List<string>();
        var inPath = new HashSet<string>();

        void Dfs(string node)
        {
            if (inPath.Contains(node))
            {
                var cycleStart = path.IndexOf(node);
                cycles.Add(path[cycleStart..].Append(node).ToList());
                return;
            }
            if (visited.Contains(node)) return;

            visited.Add(node);
            inPath.Add(node);
            path.Add(node);

            if (adjacency.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors) Dfs(neighbor);
            }

            path.RemoveAt(path.Count - 1);
            inPath.Remove(node);
        }

        foreach (var node in nodes.Where(n => !visited.Contains(n)))
        {
            Dfs(node);
        }

        return cycles;
    }

    private static string GetModuleNameFromPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var parts = dir.Replace('\\', '/').Split('/');
        return parts.LastOrDefault(p => !string.IsNullOrEmpty(p)) ?? "root";
    }

    private static string? GetModuleFromPath(string filePath, IEnumerable<string> moduleNames)
    {
        var normalized = filePath.Replace('\\', '/');
        return moduleNames.FirstOrDefault(m => normalized.StartsWith(m + "/") || normalized.Contains("/" + m + "/"));
    }
}
