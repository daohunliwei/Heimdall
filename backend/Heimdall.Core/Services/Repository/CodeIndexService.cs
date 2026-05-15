using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 代码结构索引服务——纯本地分析，不调用 LLM。
/// 使用正则表达式提取多语言符号（类、函数、接口、导出等）。
/// </summary>
public sealed partial class CodeIndexService
{
    private readonly ILogger<CodeIndexService> _logger;

    // 每块最大行数
    private const int ChunkMaxLines = 80;
    // 块间重叠行数
    private const int ChunkOverlapLines = 10;

    public CodeIndexService(ILogger<CodeIndexService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 索引整个仓库目录。
    /// </summary>
    public CodeIndexResult IndexRepository(string repoPath)
    {
        _logger.LogInformation("开始本地代码索引：{Path}", repoPath);

        var entries = new ConcurrentBag<CodeIndexEntry>();
        var allFiles = Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories);

        Parallel.ForEach(allFiles, filePath =>
        {
            var relativePath = Path.GetRelativePath(repoPath, filePath).Replace('\\', '/');

            if (ShouldSkip(relativePath)) return;

            var fileInfo = new FileInfo(filePath);
            var language = DetectLanguage(relativePath);
            var moduleName = GetModuleName(relativePath);
            var fileType = ClassifyFileType(relativePath);
            var symbols = ExtractSymbols(filePath, language);
            var deps = ExtractDependencies(filePath, language);
            var importance = CalculateImportance(relativePath, fileType);

            entries.Add(new CodeIndexEntry
            {
                FilePath = relativePath,
                ModuleName = moduleName,
                FileType = fileType,
                Language = language,
                SizeBytes = fileInfo.Length,
                ImportanceScore = importance,
                ExportedSymbols = symbols,
                DependencyHints = deps
            });
        });

        var entryList = entries.ToList();
        var moduleNames = entryList
            .Select(e => e.ModuleName)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        var sourceFiles = entryList.Where(e => e.FileType is "source" or "config").ToList();
        var moduleFileCounts = entryList
            .Where(e => !string.IsNullOrEmpty(e.ModuleName))
            .GroupBy(e => e.ModuleName)
            .ToDictionary(g => g.Key, g => g.Count());
        var entryPoints = sourceFiles
            .Where(e => e.ImportanceScore >= 8)
            .OrderByDescending(e => e.ImportanceScore)
            .Select(e => e.FilePath)
            .Take(10)
            .ToList();

        var result = new CodeIndexResult
        {
            Entries = entryList,
            ModuleNames = moduleNames,
            ModuleFileCounts = moduleFileCounts,
            EntryPointFiles = entryPoints,
            EntryPointCount = entryPoints.Count,
            ProjectType = DetectProjectType(entryList),
            TechStack = DetectTechStack(entryList),
            TotalFileCount = allFiles.Length,
            SourceFileCount = sourceFiles.Count
        };

        _logger.LogInformation("代码索引完成：{SourceFiles} 源文件, {Modules} 模块, {ProjectType}/{TechStack}",
            result.SourceFileCount, result.ModuleNames.Count, result.ProjectType, result.TechStack);

        return result;
    }

    /// <summary>
    /// 对单个文件按函数/类边界分块，返回代码块列表。
    /// </summary>
    public List<(int StartLine, int EndLine, string Content)> ChunkFile(string filePath, string language)
    {
        var chunks = new List<(int, int, string)>();
        if (!File.Exists(filePath)) return chunks;

        var lines = File.ReadAllLines(filePath);
        var boundaries = FindChunkBoundaries(lines, language);

        for (var i = 0; i < boundaries.Count; i++)
        {
            var start = boundaries[i];
            var end = Math.Min(start + ChunkMaxLines - 1, lines.Length);
            // 尝试在下一个边界处断开
            if (i + 1 < boundaries.Count && boundaries[i + 1] > start && boundaries[i + 1] <= end)
            {
                end = boundaries[i + 1] - 1;
            }
            var content = string.Join('\n', lines.Skip(start - 1).Take(end - start + 1));
            chunks.Add((start, end, content));
        }

        return chunks;
    }

    /// <summary>
    /// 查找代码块边界（函数定义、类定义等）。
    /// </summary>
    private List<int> FindChunkBoundaries(string[] lines, string language)
    {
        var boundaries = new List<int> { 1 }; // 总是从第 1 行开始
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            bool isBoundary = language switch
            {
                "csharp" => trimmed.StartsWith("public ") || trimmed.StartsWith("private ") ||
                            trimmed.StartsWith("internal ") || trimmed.StartsWith("protected ") ||
                            trimmed.StartsWith("class ") || trimmed.StartsWith("interface ") ||
                            trimmed.StartsWith("enum ") || trimmed.StartsWith("record "),
                "typescript" or "javascript" => trimmed.Contains("function ") || trimmed.Contains("=>") ||
                            trimmed.StartsWith("class ") || trimmed.StartsWith("interface ") ||
                            trimmed.StartsWith("export "),
                "python" => trimmed.StartsWith("def ") || trimmed.StartsWith("class ") ||
                            trimmed.StartsWith("async def "),
                _ => trimmed.Length > 0 && char.IsLetter(trimmed[0]) && !trimmed.StartsWith("//")
            };

            if (isBoundary && i + 1 > boundaries.Last() + ChunkMinLines)
            {
                boundaries.Add(i + 1);
            }
        }
        return boundaries;
    }

    private const int ChunkMinLines = 20;

    // ── 符号提取 ──

    private static List<string> ExtractSymbols(string filePath, string language)
    {
        var symbols = new List<string>();
        if (!File.Exists(filePath)) return symbols;

        try
        {
            var content = File.ReadAllText(filePath);
            if (content.Length > 50_000) content = content[..50_000];

            switch (language)
            {
                case "csharp":
                    symbols.AddRange(RegexPatterns.ClassPattern().Matches(content).Select(m => m.Groups[1].Value));
                    symbols.AddRange(RegexPatterns.MethodPattern().Matches(content).Select(m => m.Groups[1].Value));
                    symbols.AddRange(RegexPatterns.InterfacePattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
                case "typescript" or "javascript":
                    symbols.AddRange(RegexPatterns.TsClassPattern().Matches(content).Select(m => m.Groups[1].Value));
                    symbols.AddRange(RegexPatterns.TsFunctionPattern().Matches(content).Select(m => m.Groups[1].Value));
                    symbols.AddRange(RegexPatterns.TsExportPattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
                case "python":
                    symbols.AddRange(RegexPatterns.PyClassPattern().Matches(content).Select(m => m.Groups[1].Value));
                    symbols.AddRange(RegexPatterns.PyDefPattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
            }
        }
        catch { /* 跳过无法读取的文件 */ }

        return symbols.Distinct().Take(50).ToList();
    }

    private static List<string> ExtractDependencies(string filePath, string language)
    {
        var deps = new List<string>();
        try
        {
            var content = File.ReadAllText(filePath);
            if (content.Length > 50_000) content = content[..50_000];

            switch (language)
            {
                case "csharp":
                    deps.AddRange(RegexPatterns.UsingPattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
                case "typescript" or "javascript":
                    deps.AddRange(RegexPatterns.ImportPattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
                case "python":
                    deps.AddRange(RegexPatterns.PyImportPattern().Matches(content).Select(m => m.Groups[1].Value));
                    break;
            }
        }
        catch { }

        return deps.Distinct().Take(30).ToList();
    }

    // ── 辅助方法 ──

    private static bool ShouldSkip(string relativePath)
    {
        var parts = relativePath.Split('/');
        foreach (var part in parts)
        {
            if (part is ".git" or "node_modules" or "bin" or "obj" or ".vs" or "dist" or "build"
                or ".next" or "coverage" or ".nyc_output" or "__pycache__" or ".pytest_cache"
                or "vendor" or "target" or ".gradle")
                return true;
        }
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        if (ext is ".dll" or ".exe" or ".so" or ".dylib" or ".pdb" or ".obj" or ".o"
            or ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".svg"
            or ".woff" or ".woff2" or ".ttf" or ".eot"
            or ".zip" or ".tar" or ".gz" or ".7z" or ".rar"
            or ".lock" or ".min.js" or ".min.css" or ".map")
            return true;
        return false;
    }

    private static string DetectLanguage(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".ts" or ".tsx" => "typescript",
            ".js" or ".jsx" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".rb" => "ruby",
            ".php" => "php",
            ".c" or ".h" => "c",
            ".cpp" or ".hpp" or ".cc" or ".cxx" => "cpp",
            _ => "other"
        };
    }

    private static string GetModuleName(string relativePath)
    {
        var firstSlash = relativePath.IndexOf('/');
        if (firstSlash > 0)
        {
            var root = relativePath[..firstSlash];
            if (root is not "src" and not "test" and not "tests" and not "lib" and not "docs" and not "doc")
                return root;
            // 对于 src 等通用根目录，取第二级
            var secondSlash = relativePath.IndexOf('/', firstSlash + 1);
            if (secondSlash > 0)
                return relativePath[(firstSlash + 1)..secondSlash];
        }
        return "root";
    }

    private static string ClassifyFileType(string filePath)
    {
        var lower = filePath.ToLowerInvariant();
        if (lower.Contains("test") || lower.Contains("spec") || lower.EndsWith(".test.ts") || lower.EndsWith(".spec.ts"))
            return "test";
        if (lower.Contains(".generated.") || lower.EndsWith(".g.cs") || lower.EndsWith(".gen.go"))
            return "generated";
        if (lower.EndsWith(".md") || lower.EndsWith(".txt") || lower.EndsWith(".rst"))
            return "doc";
        return "source";
    }

    private static int CalculateImportance(string filePath, string fileType)
    {
        var lower = filePath.ToLowerInvariant();
        var score = 5;

        // 入口文件 +10
        if (Regex.IsMatch(lower, @"(program\.cs|main\.go|index\.(ts|js|tsx)|app\.(tsx|jsx|py)|__init__\.py|setup\.py)$"))
            score += 10;
        // 核心目录 +3
        if (lower.Contains("src/") || lower.Contains("lib/") || lower.Contains("app/"))
            score += 3;
        // 接口/抽象 +2
        if (Regex.IsMatch(lower, @"i\w+\.cs$") && lower.Contains("interface"))
            score += 2;
        // 测试文件扣分
        if (fileType == "test")
            score -= 3;
        // 配置文件基础分
        if (fileType == "config")
            score = 3;
        // 文档
        if (fileType == "doc")
            score = 1;

        return Math.Max(1, Math.Min(15, score));
    }

    private static string DetectProjectType(List<CodeIndexEntry> entries)
    {
        var paths = entries.Select(e => e.FilePath.ToLowerInvariant()).ToList();
        if (paths.Any(p => p.EndsWith(".csproj"))) return "dotnet";
        if (paths.Any(p => p is "package.json")) return "node";
        if (paths.Any(p => p is "go.mod")) return "go";
        if (paths.Any(p => p is "Cargo.toml")) return "rust";
        if (paths.Any(p => p.EndsWith(".py"))) return "python";
        if (paths.Any(p => p.EndsWith(".java"))) return "java";
        return "generic";
    }

    private static string DetectTechStack(List<CodeIndexEntry> entries)
    {
        var paths = entries.Select(e => e.FilePath.ToLowerInvariant()).ToList();
        var parts = new List<string>();
        if (paths.Any(p => p.EndsWith(".csproj"))) parts.Add(".NET");
        if (paths.Any(p => p.EndsWith(".tsx"))) parts.Add("React+TypeScript");
        if (paths.Any(p => p.EndsWith("next.config."))) parts.Add("Next.js");
        if (paths.Any(p => p.EndsWith(".py"))) parts.Add("Python");
        if (paths.Any(p => p.EndsWith(".go"))) parts.Add("Go");
        return parts.Count > 0 ? string.Join("/", parts) : "未知";
    }

    public static int CalculateRecommendedPageCount(int moduleCount, int entryPointCount)
        => Math.Max(4, Math.Min(60, moduleCount * 2 + entryPointCount));
}

/// <summary>
/// 用于符号提取的正则表达式模式。
/// </summary>
internal static partial class RegexPatterns
{
    // C#
    [GeneratedRegex(@"class\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex ClassPattern();
    [GeneratedRegex(@"(?:public|private|internal|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<\w+>)?)\s+(\w+)\s*\(", RegexOptions.Compiled)]
    public static partial Regex MethodPattern();
    [GeneratedRegex(@"interface\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex InterfacePattern();
    [GeneratedRegex(@"using\s+([\w.]+)", RegexOptions.Compiled)]
    public static partial Regex UsingPattern();

    // TypeScript/JavaScript
    [GeneratedRegex(@"class\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex TsClassPattern();
    [GeneratedRegex(@"(?:function|const|let|var)\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex TsFunctionPattern();
    [GeneratedRegex(@"export\s+(?:const|function|class|interface|type|enum)\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex TsExportPattern();
    [GeneratedRegex(@"from\s+['""]([^'""]+)['""]", RegexOptions.Compiled)]
    public static partial Regex ImportPattern();

    // Python
    [GeneratedRegex(@"class\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex PyClassPattern();
    [GeneratedRegex(@"def\s+(\w+)", RegexOptions.Compiled)]
    public static partial Regex PyDefPattern();
    [GeneratedRegex(@"(?:from|import)\s+([\w.]+)", RegexOptions.Compiled)]
    public static partial Regex PyImportPattern();
}
