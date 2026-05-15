using System.Text;
using System.Text.Json;
using Heimdall.Core.Interfaces.Services;
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
    private readonly IPromptMergeService _promptMergeService;
    private readonly ILogger<CodeSummaryService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CodeSummaryService(
        TaskLlmService llmService,
        IPromptMergeService promptMergeService,
        ILogger<CodeSummaryService> logger)
    {
        _llmService = llmService;
        _promptMergeService = promptMergeService;
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

        _logger.LogInformation("代码摘要-开始处理: {FileCount} 个源文件", sourceFiles.Count);

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
        var prompt = await _promptMergeService.BuildPromptAsync(
            "code_summary", provider ?? "ollama", "text",
            new Dictionary<string, string>
            {
                ["project_type"] = input.ProjectType ?? "",
                ["tech_stack"] = input.TechStack ?? "",
                ["total_files"] = input.TotalFileCount.ToString(),
                ["module_count"] = input.ModuleNames.Count.ToString(),
                ["entry_points"] = string.Join(", ", input.EntryPointFiles.Take(5)),
                ["module_descriptions"] = string.Join("\n", input.ModuleDescriptions.Select(kv => $"- **{kv.Key}**: {kv.Value}")),
                ["language"] = language
            },
            subCategory: "system");
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

            _logger.LogDebug("代码摘要-读取文件: {FilePath}", entry.FilePath);
            var content = await File.ReadAllTextAsync(fullPath, ct);
            // 限制文件内容不超过 3000 字符
            if (content.Length > 3000)
                content = content[..3000] + "\n// ... (truncated)";

            _logger.LogDebug("代码摘要-构建提示词: {FilePath}", entry.FilePath);
            var prompt = await _promptMergeService.BuildPromptAsync(
                "code_summary", provider ?? "ollama", "text",
                new Dictionary<string, string>
                {
                    ["file_path"] = entry.FilePath,
                    ["language"] = language,
                    ["content"] = content
                },
                subCategory: "file");

            _logger.LogInformation("代码摘要-LLM调用: {FilePath} PromptLen={PromptLen}", entry.FilePath, prompt?.Length ?? 0);
            var response = await _llmService.GenerateTextAsync(
                provider ?? "ollama", model ?? "gemma4:e2b", null,
                prompt, ct);
            _logger.LogInformation("代码摘要-LLM完成: {FilePath} ResponseLen={ResponseLen}", entry.FilePath, response?.Length ?? 0);

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

        var prompt = await _promptMergeService.BuildPromptAsync(
            "code_summary", provider ?? "ollama", "text",
            new Dictionary<string, string>
            {
                ["module_name"] = moduleName,
                ["key_files"] = string.Join(", ", keyFiles),
                ["file_summaries"] = context.ToString(),
                ["language"] = language
            },
            subCategory: "module");

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

    // BuildSystemSummaryPrompt 已迁移至 IPromptMergeService (V5)
    // 提示词内容见 PromptSeedData.cs — code-summary-system 模板
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
