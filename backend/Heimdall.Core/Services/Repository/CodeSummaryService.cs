using System.Text;
using System.Text.Json;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Tasks;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 代码分层摘要服务——文件级 → 模块级 → 系统级。
/// </summary>
public sealed class CodeSummaryService
{
    private const int FileBatchSize = 10;
    private readonly TaskLlmService _llmService;
    private readonly ILogger<CodeSummaryService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CodeSummaryService(TaskLlmService llmService, ILogger<CodeSummaryService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// 对关键源文件生成文件级摘要（批量并行）。
    /// </summary>
    public async Task<List<FileSummary>> GenerateFileSummariesAsync(
        List<CodeIndexEntry> entries,
        string repoPath,
        string? provider,
        string? model,
        string language,
        CancellationToken ct)
    {
        var sourceFiles = entries
            .Where(e => e.FileType is "source" or "config")
            .OrderByDescending(e => e.ImportanceScore)
            .ToList();

        var results = new List<FileSummary>();

        for (var i = 0; i < sourceFiles.Count; i += FileBatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = sourceFiles.Skip(i).Take(FileBatchSize).ToList();
            var batchTasks = batch.Select(entry =>
                SummarizeFileAsync(entry, repoPath, provider, model, language, ct));

            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults.Where(r => r is not null)!);

            var percent = Math.Min(100, (i + FileBatchSize) * 100 / sourceFiles.Count);
            _logger.LogDebug("文件摘要进度：{Percent}%（{Done}/{Total}）",
                percent, results.Count, sourceFiles.Count);
        }

        _logger.LogInformation("文件摘要完成：{Count} 个文件", results.Count);
        return results;
    }

    /// <summary>
    /// 将文件级摘要聚合为模块级摘要。
    /// </summary>
    public async Task<List<ModuleSummary>> GenerateModuleSummariesAsync(
        List<FileSummary> fileSummaries,
        List<CodeIndexEntry> entries,
        string? provider,
        string? model,
        string language,
        CancellationToken ct)
    {
        var moduleGroups = entries
            .Where(e => e.FileType is "source" or "config")
            .GroupBy(e => e.ModuleName)
            .ToList();

        var results = new List<ModuleSummary>();

        foreach (var group in moduleGroups)
        {
            ct.ThrowIfCancellationRequested();
            var moduleFiles = group.ToList();
            var moduleFileSummaries = fileSummaries
                .Where(fs => moduleFiles.Any(mf => mf.FilePath == fs.FilePath))
                .ToList();

            var keyFiles = moduleFiles
                .OrderByDescending(f => f.ImportanceScore)
                .Take(5)
                .Select(f => f.FilePath)
                .ToList();

            var summary = await GenerateModuleSummaryAsync(
                group.Key, moduleFileSummaries, keyFiles,
                provider, model, language, ct);

            if (summary is not null) results.Add(summary);
        }

        _logger.LogInformation("模块摘要完成：{Count} 个模块", results.Count);
        return results;
    }

    /// <summary>
    /// 将所有模块摘要聚合为系统级架构概述。
    /// </summary>
    public async Task<SystemSummary?> GenerateSystemSummaryAsync(
        SystemSummaryInput input,
        string? provider,
        string? model,
        string language,
        CancellationToken ct)
    {
        var prompt = BuildSystemSummaryPrompt(input, language);
        var response = await _llmService.GenerateTextAsync(
            provider ?? "ollama", model ?? "gemma4:e2b", null,
            prompt, ct);

        if (string.IsNullOrWhiteSpace(response)) return null;

        return new SystemSummary
        {
            ProjectType = input.ProjectType,
            TechStack = input.TechStack,
            ArchitectureOverview = response,
            CoreComponents = input.CoreComponents,
            TotalFileCount = input.TotalFileCount,
            ModuleCount = input.ModuleNames.Count,
            EntryPointCount = input.EntryPointFiles.Count
        };
    }

    // ── 内部方法 ──

    private async Task<FileSummary?> SummarizeFileAsync(
        CodeIndexEntry entry,
        string repoPath,
        string? provider,
        string? model,
        string language,
        CancellationToken ct)
    {
        try
        {
            var fullPath = Path.Combine(repoPath, entry.FilePath);
            if (!File.Exists(fullPath)) return null;

            var content = await File.ReadAllTextAsync(fullPath, ct);
            // 限制文件内容不超过 3000 字符
            if (content.Length > 3000)
                content = content[..3000] + "\n// ... (truncated)";

            var prompt = $"""
You are a code analyst. Provide a 1-3 sentence summary of this file.
File: {entry.FilePath}
Language hint: {language}

<code>
{content}
</code>

Summary (1-3 sentences, in {language}):
""";

            var response = await _llmService.GenerateTextAsync(
                provider ?? "ollama", model ?? "gemma4:e2b", null,
                prompt, ct);

            if (string.IsNullOrWhiteSpace(response)) return null;

            return new FileSummary
            {
                FilePath = entry.FilePath,
                Summary = response.Trim(),
                TokenCount = content.Length / 4 // 近似
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "文件摘要失败：{FilePath}", entry.FilePath);
            return null;
        }
    }

    private async Task<ModuleSummary?> GenerateModuleSummaryAsync(
        string moduleName,
        List<FileSummary> fileSummaries,
        List<string> keyFiles,
        string? provider,
        string? model,
        string language,
        CancellationToken ct)
    {
        var context = new StringBuilder();
        foreach (var fs in fileSummaries.Take(10))
        {
            context.AppendLine($"- {fs.FilePath}: {fs.Summary}");
        }

        var prompt = $"""
You are a software architect. Based on the following file summaries for the "{moduleName}" module,
provide a 3-5 sentence description of this module's responsibilities.

Key files: {string.Join(", ", keyFiles)}

<file_summaries>
{context}
</file_summaries>

Module description (3-5 sentences, in {language}):
""";

        var response = await _llmService.GenerateTextAsync(
            provider ?? "ollama", model ?? "gemma4:e2b", null,
            prompt, ct);

        if (string.IsNullOrWhiteSpace(response)) return null;

        return new ModuleSummary
        {
            ModuleName = moduleName,
            Summary = response.Trim(),
            KeyFiles = keyFiles,
            FileCount = fileSummaries.Count
        };
    }

    private static string BuildSystemSummaryPrompt(SystemSummaryInput input, string language)
    {
        var moduleContext = new StringBuilder();
        foreach (var (name, desc) in input.ModuleDescriptions)
        {
            moduleContext.AppendLine($"- **{name}**: {desc}");
        }

        return $"""
You are a senior software architect. Based on the following analysis of a {input.ProjectType} repository
({input.TechStack}), provide a comprehensive architecture overview.

Total files: {input.TotalFileCount}, Modules: {input.ModuleNames.Count}

Entry points: {string.Join(", ", input.EntryPointFiles.Take(5))}

<module_descriptions>
{moduleContext}
</module_descriptions>

Provide a system architecture overview covering:
1. Overall architecture pattern (MVC, microservices, monolith, etc.)
2. Core components and their interactions
3. Key data flows
4. Design decisions evident from the code structure

Respond in {language}.
""";
    }
}

public class SystemSummaryInput
{
    public string ProjectType { get; init; } = string.Empty;
    public string TechStack { get; init; } = string.Empty;
    public int TotalFileCount { get; init; }
    public List<string> ModuleNames { get; init; } = new();
    public List<string> EntryPointFiles { get; init; } = new();
    public List<string> CoreComponents { get; init; } = new();
    public Dictionary<string, string> ModuleDescriptions { get; init; } = new();
}
