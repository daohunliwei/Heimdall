using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 代码结构索引服务——解析 file tree、识别项目类型/技术栈、按目录分区模块。
/// 纯本地计算，不依赖 LLM。
/// </summary>
public sealed class CodeStructureIndexService
{
    private readonly ILogger<CodeStructureIndexService> _logger;

    // 标志性文件 → (项目类型, 技术栈)
    private static readonly Dictionary<string, (string ProjectType, string TechStack)> TechDetectors = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csproj"] = (".NET", "C# / .NET"),
        ["package.json"] = ("Node.js", "JavaScript / TypeScript"),
        ["go.mod"] = ("Go", "Go"),
        ["Cargo.toml"] = ("Rust", "Rust"),
        ["pom.xml"] = ("Java Maven", "Java"),
        ["build.gradle"] = ("Java Gradle", "Java / Kotlin"),
        ["requirements.txt"] = ("Python", "Python"),
        ["pyproject.toml"] = ("Python", "Python"),
        ["Dockerfile"] = ("Containerized", "Docker"),
        ["CMakeLists.txt"] = ("C/C++", "C / C++"),
        ["Makefile"] = ("C/C++", "C / C++ / Make"),
    };

    // 应跳过的目录
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", "dist", "build", ".next",
        "__pycache__", ".venv", "venv", "vendor", "target", ".idea", ".vs",
        ".vscode", "coverage", ".nyc_output", "tmp", "temp", ".cache", ".trae"
    };

    // 应跳过的文件扩展名
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".so", ".dylib", ".bin", ".obj", ".o", ".a", ".lib",
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".bmp", ".webp",
        ".woff", ".woff2", ".ttf", ".eot", ".otf",
        ".zip", ".tar", ".gz", ".bz2", ".7z", ".rar",
        ".lock", ".sum", ".snap",
        ".pb.go", ".pb.cc", ".generated.cs", ".designer.cs", ".g.cs", ".g.i.cs"
    };

    // 应跳过的基础文件名
    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "Gemfile.lock",
        "poetry.lock", "Cargo.lock", ".DS_Store", "Thumbs.db"
    };

    public CodeStructureIndexService(ILogger<CodeStructureIndexService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 对仓库本地副本执行结构索引。
    /// </summary>
    public CodeIndexResult IndexRepository(string repoPath)
    {
        var allFiles = Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories);
        var entries = new List<CodeIndexEntry>();
        var projectType = "Unknown";
        var techStack = "Unknown";

        foreach (var fullPath in allFiles)
        {
            var relativePath = Path.GetRelativePath(repoPath, fullPath).Replace('\\', '/');

            // 检查是否应跳过
            if (ShouldSkipFile(relativePath)) continue;

            var fileInfo = new FileInfo(fullPath);
            var fileType = ClassifyFileType(relativePath);
            var moduleName = DetermineModule(relativePath);
            var entry = new CodeIndexEntry
            {
                FilePath = relativePath,
                ModuleName = moduleName,
                FileType = fileType,
                SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                DependencyHints = new List<string>(),
                ImportanceScore = CalculateImportance(fileType, relativePath)
            };

            entries.Add(entry);

            // 检测技术栈：先按完整文件名匹配（Dockerfile/Makefile），再按扩展名匹配（.csproj/go.mod）
            var fileName = Path.GetFileName(relativePath);
            var ext = Path.GetExtension(relativePath);
            if (TechDetectors.TryGetValue(fileName, out var detected) ||
                TechDetectors.TryGetValue(ext, out detected))
            {
                projectType = detected.ProjectType;
                techStack = detected.TechStack;
            }
        }

        var modules = entries
            .GroupBy(e => e.ModuleName)
            .Select(g => new { ModuleName = g.Key, FileCount = g.Count() })
            .OrderByDescending(m => m.FileCount)
            .ToList();

        var sourceFiles = entries.Where(e => e.FileType == "source" || e.FileType == "config").ToList();
        var entryPoints = IdentifyEntryPoints(entries);

        _logger.LogInformation(
            "结构索引完成：{TotalFiles} 个文件，{ModuleCount} 个模块，项目类型={ProjectType}，技术栈={TechStack}",
            entries.Count, modules.Count, projectType, techStack);

        return new CodeIndexResult
        {
            Entries = entries,
            ModuleNames = modules.Select(m => m.ModuleName).ToList(),
            ModuleFileCounts = modules.ToDictionary(m => m.ModuleName, m => m.FileCount),
            ProjectType = projectType,
            TechStack = techStack,
            TotalFileCount = entries.Count,
            SourceFileCount = sourceFiles.Count,
            EntryPointCount = entryPoints.Count,
            EntryPointFiles = entryPoints
        };
    }

    /// <summary>
    /// 计算推荐页面数量——综合模块数、入口点、设计模式数量和调用图深度。范围 15-80 页。
    /// </summary>
    public static int CalculateRecommendedPageCount(
        int moduleCount, int entryPointCount, int patternCount, int callGraphDepth)
    {
        var rawCount = moduleCount * 3 + entryPointCount * 2 + patternCount * 2 + callGraphDepth * 3;
        return Math.Max(15, Math.Min(80, rawCount));
    }

    /// <summary>
    /// 根据文件数量计算最大层深。
    /// files &lt; 50 → 2 层；50-200 → 3 层；200-500 → 4 层；&gt; 500 → 5 层。
    /// </summary>
    public static int CalculateMaxDepth(int totalFileCount)
    {
        return totalFileCount switch
        {
            < 50 => 2,
            < 200 => 3,
            < 500 => 4,
            _ => 5
        };
    }

    // ── 内部方法 ──

    private static bool ShouldSkipFile(string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Any(p => ExcludedDirs.Contains(p))) return true;

        var ext = Path.GetExtension(relativePath);
        if (ExcludedExtensions.Contains(ext)) return true;

        var fileName = Path.GetFileName(relativePath);
        if (ExcludedFileNames.Contains(fileName)) return true;

        // 跳过大文件 (>500KB 可能是数据文件)
        // 此项在实际索引中由调用方控制，这里返回 false

        return false;
    }

    private static string ClassifyFileType(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();

        if (fileName is "makefile" or "dockerfile" or ".dockerignore" or ".gitignore" or ".editorconfig")
            return "config";

        if (fileName.StartsWith('.') && ext != ".cs" && ext != ".ts" && ext != ".js" && ext != ".py")
            return "config";

        if (relativePath.Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("/spec/", StringComparison.OrdinalIgnoreCase))
            return "test";

        if (relativePath.Contains("/doc/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("/docs/", StringComparison.OrdinalIgnoreCase) ||
            ext is ".md" or ".rst" or ".txt" or ".adoc")
            return "doc";

        if (relativePath.Contains("/generated/", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".generated.cs") || fileName.EndsWith(".g.cs"))
            return "generated";

        if (ext is ".json" or ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".config" or ".xml")
            return "config";

        return "source";
    }

    private static string DetermineModule(string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Length <= 1) return "root";
        // 取第一层目录作为模块名
        return parts[0];
    }

    private static int CalculateImportance(string fileType, string relativePath)
    {
        var score = fileType switch
        {
            "source" => 5,
            "config" => 4,
            "test" => 2,
            "doc" => 3,
            _ => 1
        };

        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        if (fileName is "program.cs" or "main.go" or "index.ts" or "index.js" or "app.tsx" or "_app.tsx")
            score += 10;

        if (fileName is "startup.cs" or "host.cs" or "server.ts" or "server.js")
            score += 8;

        if (relativePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
            score += 2;

        return score;
    }

    private static List<string> IdentifyEntryPoints(List<CodeIndexEntry> entries)
    {
        var entryFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "program.cs", "main.go", "index.ts", "index.js", "main.py",
            "app.tsx", "_app.tsx", "server.ts", "server.js", "app.js",
            "main.rs", "main.java", "main.cpp", "main.c"
        };

        return entries
            .Where(e => entryFileNames.Contains(Path.GetFileName(e.FilePath)))
            .Select(e => e.FilePath)
            .ToList();
    }
}

/// <summary>
/// 代码结构索引结果。
/// </summary>
public class CodeIndexResult
{
    public List<CodeIndexEntry> Entries { get; init; } = new();
    public List<string> ModuleNames { get; init; } = new();
    public Dictionary<string, int> ModuleFileCounts { get; init; } = new();
    public string ProjectType { get; init; } = string.Empty;
    public string TechStack { get; init; } = string.Empty;
    public int TotalFileCount { get; init; }
    public int SourceFileCount { get; init; }
    public int EntryPointCount { get; init; }
    public List<string> EntryPointFiles { get; init; } = new();
}
