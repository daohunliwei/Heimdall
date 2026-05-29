using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 代码结构索引服务——基于 TreeSitter.DotNet 统一解析引擎。
/// </summary>
public sealed partial class CodeIndexService
{
    private readonly ILogger<CodeIndexService> _logger;
    private readonly TreeSitterAnalyzer _analyzer;

    private const int ChunkMaxLinesWithBoundary = 120;
    private const int ChunkMaxLines = 80;
    private const int ChunkMinLines = 20;

    /// <summary>
    /// 附带 AST 上下文的分块结果
    /// </summary>
    public sealed record CodeChunkWithAstContext(
        int StartLine,
        int EndLine,
        string Content,
        string? OwningClass,
        string? OwningMethod,
        string[]? Modifiers,
        string[]? CallerSymbols,
        string[]? CalleeSymbols);

    public CodeIndexService(ILogger<CodeIndexService> logger, TreeSitterAnalyzer? analyzer = null)
    {
        _logger = logger;
        _analyzer = analyzer ?? new TreeSitterAnalyzer(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<TreeSitterAnalyzer>());
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

            string source;
            try { source = File.ReadAllText(filePath); }
            catch { return; }

            var result = _analyzer.Analyze(filePath, source, language);
            var moduleName = GetModuleName(relativePath);
            var fileType = ClassifyFileType(relativePath);
            var importance = CalculateImportance(relativePath, fileType);
            var symbols = result.Symbols.Select(s => s.FullSignature).Distinct().Take(100).ToList();
            var deps = result.CallEdges.Select(e => e.CalleeFilePath).Distinct().Take(30).ToList();

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
    /// 构建 AST 持久化投影——全量解析仓库所有源文件并产出可直接序列化的结构化数据。
    /// </summary>
    public AstPersistenceProjection BuildPersistenceProjection(string repoPath)
    {
        _logger.LogInformation("开始构建 AST 持久化投影：{Path}", repoPath);

        var fileResults = new ConcurrentBag<AstFileResult>();
        var allFiles = Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories);

        Parallel.ForEach(allFiles, filePath =>
        {
            var relativePath = Path.GetRelativePath(repoPath, filePath).Replace('\\', '/');
            if (ShouldSkip(relativePath)) return;

            var language = DetectLanguage(relativePath);
            if (language == "other") return;

            string source;
            try { source = File.ReadAllText(filePath); }
            catch { return; }

            var result = _analyzer.Analyze(filePath, source, language);
            fileResults.Add(result);
        });

        var resultsList = fileResults.ToList();
        var symbolNames = new List<SymbolNameEntry>();
        var fileList = new List<FileListEntry>();
        int totalSymbols = 0, totalCallEdges = 0, totalChunks = 0;

        foreach (var fr in resultsList)
        {
            var relativePath = fr.FilePath;
            if (string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(fr.FilePath))
                relativePath = fr.FilePath;

            totalSymbols += fr.Symbols.Count;
            totalCallEdges += fr.CallEdges.Count;
            totalChunks += fr.Chunks.Count;

            fileList.Add(new FileListEntry(fr.FilePath, fr.Language, fr.Symbols.Count));

            foreach (var sym in fr.Symbols.Take(200))
            {
                symbolNames.Add(new SymbolNameEntry(sym.Name, sym.Kind, fr.FilePath));
            }
        }

        _logger.LogInformation("AST 持久化投影完成：{Files} 文件, {Symbols} 符号, {Edges} 调用边, {Chunks} 分块",
            resultsList.Count, totalSymbols, totalCallEdges, totalChunks);

        return new AstPersistenceProjection
        {
            FileResults = resultsList,
            SymbolNames = symbolNames,
            FileList = fileList,
            TotalFiles = resultsList.Count,
            TotalSymbols = totalSymbols,
            TotalCallEdges = totalCallEdges,
            TotalChunks = totalChunks
        };
    }

    /// <summary>
    /// 对单个文件按 AST 节点边界分块，附带 AST 上下文元数据（所属类、调用关系、修饰符）。
    /// </summary>
    public List<CodeChunkWithAstContext> ChunkFileWithAstContext(string filePath, string language)
    {
        var chunks = new List<CodeChunkWithAstContext>();
        if (!File.Exists(filePath)) return chunks;

        try
        {
            var source = File.ReadAllText(filePath);
            var result = _analyzer.Analyze(filePath, source, language);

            foreach (var chunk in result.Chunks)
            {
                // 查找覆盖此分块的符号
                var overlappingSymbols = result.Symbols
                    .Where(s => s.StartLine <= chunk.EndLine && s.EndLine >= chunk.StartLine)
                    .ToList();

                var owningType = overlappingSymbols
                    .FirstOrDefault(s => s.Kind is "class" or "interface" or "struct" or "record");
                var owningMethod = overlappingSymbols
                    .FirstOrDefault(s => s.Kind is "method" or "function" or "constructor");
                var modifiers = owningMethod?.Modifiers ?? owningType?.Modifiers;

                // 查找调用关系
                var symbolName = owningMethod?.Name ?? owningType?.Name;
                var callers = string.IsNullOrWhiteSpace(symbolName) ? null :
                    result.CallEdges
                        .Where(e => e.CalleeSymbol == symbolName ||
                                    e.CalleeSymbol == $"{owningType?.Name}.{symbolName}")
                        .Select(e => e.CallerSymbol)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct()
                        .Take(5)
                        .ToArray();

                var callees = string.IsNullOrWhiteSpace(symbolName) ? null :
                    result.CallEdges
                        .Where(e => e.CallerSymbol == symbolName ||
                                    e.CallerSymbol == $"{owningType?.Name}.{symbolName}")
                        .Select(e => e.CalleeSymbol)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct()
                        .Take(5)
                        .ToArray();

                chunks.Add(new CodeChunkWithAstContext(
                    chunk.StartLine,
                    chunk.EndLine,
                    chunk.Content,
                    owningType?.Name,
                    owningMethod?.Name,
                    modifiers?.Length > 0 ? modifiers : null,
                    callers?.Length > 0 ? callers : null,
                    callees?.Length > 0 ? callees : null));
            }

            if (chunks.Count == 0)
            {
                // 回退：按固定行分块（无 AST 上下文）
                var lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i += ChunkMaxLines)
                {
                    var end = Math.Min(i + ChunkMaxLines, lines.Length);
                    chunks.Add(new CodeChunkWithAstContext(
                        i + 1, end, string.Join('\n', lines[i..end]),
                        null, null, null, null, null));
                }
            }
        }
        catch
        {
            // 无法读取或解析，返回空
        }

        return chunks;
    }

    /// <summary>
    /// 对单个文件按 AST 节点边界分块（兼容旧签名）。
    /// </summary>
    public List<(int StartLine, int EndLine, string Content)> ChunkFile(string filePath, string language)
    {
        return ChunkFileWithAstContext(filePath, language)
            .Select(c => (c.StartLine, c.EndLine, c.Content))
            .ToList();
    }

    // ── 辅助方法 ──

    private static bool ShouldSkip(string relativePath)
    {
        var parts = relativePath.Split('/');
        foreach (var part in parts)
        {
            if (part is ".git" or "node_modules" or "bin" or "obj" or ".vs" or "dist" or "build"
                or ".next" or "coverage" or ".nyc_output" or "__pycache__" or ".pytest_cache"
                or "vendor" or "target" or ".gradle" or ".trae")
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
            ".swift" => "swift",
            ".scala" => "scala",
            ".hs" => "haskell",
            ".html" or ".htm" => "html",
            ".css" or ".scss" or ".less" => "css",
            ".json" => "json",
            ".sh" or ".bash" => "bash",
            ".toml" => "toml",
            ".ml" => "ocaml",
            ".jl" => "julia",
            ".axaml" or ".xaml" => "xml",
            ".csproj" or ".props" or ".targets" => "xml",
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
        if (Regex.IsMatch(lower, @"(program\.cs|main\.go|index\.(ts|js|tsx)|app\.(tsx|jsx|py)|__init__\.py|setup\.py)$"))
            score += 10;
        if (lower.Contains("src/") || lower.Contains("lib/") || lower.Contains("app/"))
            score += 3;
        if (Regex.IsMatch(lower, @"i\w+\.cs$") && lower.Contains("interface"))
            score += 2;
        if (fileType == "test") score -= 3;
        if (fileType == "config") score = 3;
        if (fileType == "doc") score = 1;
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
}
