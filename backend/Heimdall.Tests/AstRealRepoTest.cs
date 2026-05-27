using System.Text.Json;
using Heimdall.Core.Services.Repository;
using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class AstRealRepoTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// 对真实仓库运行 AST 分析，将完整结果写入临时文件用于验证。
    /// 设置 HEIMDALL_AST_TEST_REPO_PATH 环境变量指向要测试的仓库路径。
    /// 默认使用 Heimdall 自身仓库。
    /// </summary>
    [Fact]
    public void AnalyzeRealRepo_AndWriteFullResultToTempFile()
    {
        var repoPath = Environment.GetEnvironmentVariable("HEIMDALL_AST_TEST_REPO_PATH")
            ?? FindHeimdallRoot();

        Assert.True(Directory.Exists(repoPath), $"仓库路径不存在: {repoPath}");

        var analyzer = new TreeSitterAnalyzer(new NullLogger<TreeSitterAnalyzer>());
        var codeIndexService = new CodeIndexService(new NullLogger<CodeIndexService>(), analyzer);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var projection = codeIndexService.BuildPersistenceProjection(repoPath);
        sw.Stop();

        // 序列化全量结果
        var fullJson = JsonSerializer.Serialize(new
        {
            repository_path = repoPath,
            elapsed_ms = sw.ElapsedMilliseconds,
            total_files = projection.TotalFiles,
            total_symbols = projection.TotalSymbols,
            total_call_edges = projection.TotalCallEdges,
            total_chunks = projection.TotalChunks,
            symbol_summary = projection.SymbolNames
                .GroupBy(s => s.Kind)
                .ToDictionary(g => g.Key, g => g.Count()),
            language_breakdown = projection.FileList
                .GroupBy(f => f.Language)
                .ToDictionary(g => g.Key, g => g.Count()),
            file_list = projection.FileList.OrderByDescending(f => f.SymbolCount).Take(50),
            file_results = projection.FileResults
        }, JsonOptions);

        var outputPath = Path.Combine(Path.GetTempPath(), $"heimdall_ast_full_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(outputPath, fullJson);

        var outputSize = new FileInfo(outputPath).Length;

        // 也写一份轻量摘要
        var summaryPath = Path.Combine(Path.GetTempPath(), $"heimdall_ast_summary_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var summaryJson = JsonSerializer.Serialize(new
        {
            repository_path = repoPath,
            elapsed_ms = sw.ElapsedMilliseconds,
            total_files = projection.TotalFiles,
            total_symbols = projection.TotalSymbols,
            total_call_edges = projection.TotalCallEdges,
            total_chunks = projection.TotalChunks,
            symbol_summary = projection.SymbolNames
                .GroupBy(s => s.Kind)
                .ToDictionary(g => g.Key, g => g.Count()),
            language_breakdown = projection.FileList
                .GroupBy(f => f.Language)
                .ToDictionary(g => g.Key, g => g.Count()),
            top_files_by_symbols = projection.FileList
                .OrderByDescending(f => f.SymbolCount)
                .Take(30)
                .Select(f => new { f.Path, f.Language, f.SymbolCount }),
            top_symbols = projection.SymbolNames
                .Where(s => s.Kind is "class" or "interface")
                .GroupBy(s => s.Name)
                .Select(g => new { name = g.Key, files = g.Select(x => x.File).Distinct().Count() })
                .OrderByDescending(x => x.files)
                .Take(50)
        }, JsonOptions);
        File.WriteAllText(summaryPath, summaryJson);

        // 验证基本断言
        Assert.True(projection.TotalFiles > 0, "应至少分析到 1 个文件");
        Assert.True(projection.TotalSymbols > 0, "应至少提取到 1 个符号");
        Assert.NotEmpty(projection.FileResults);
        Assert.True(outputSize > 1024, $"全量 JSON 应超过 1KB，实际: {outputSize} bytes");

        // 输出路径到测试输出
        Console.WriteLine($"=== AST ANALYSIS COMPLETE ===");
        Console.WriteLine($"Repo: {repoPath}");
        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Files: {projection.TotalFiles}");
        Console.WriteLine($"Symbols: {projection.TotalSymbols}");
        Console.WriteLine($"CallEdges: {projection.TotalCallEdges}");
        Console.WriteLine($"Chunks: {projection.TotalChunks}");
        Console.WriteLine($"Full JSON: {outputPath} ({outputSize / 1024} KB)");
        Console.WriteLine($"Summary JSON: {summaryPath}");
        Console.WriteLine($"Languages: {string.Join(", ", projection.FileList.Select(f => f.Language).Distinct().OrderBy(l => l))}");
    }

    private static string FindHeimdallRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Heimdall.sln")) ||
                Directory.Exists(Path.Combine(dir, "backend")) &&
                Directory.Exists(Path.Combine(dir, "frontend")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return AppContext.BaseDirectory;
    }
}
