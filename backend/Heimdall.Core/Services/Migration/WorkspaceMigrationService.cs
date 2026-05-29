using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Heimdall.Core.Services.Migration;

/// <summary>
/// 一次性数据迁移服务：将现有 DB TEXT 列内容写入 Workspace 文件系统。
/// </summary>
public sealed class WorkspaceMigrationService
{
    private readonly ISqlSugarClient _db;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<WorkspaceMigrationService> _logger;

    public WorkspaceMigrationService(
        ISqlSugarClient db,
        WorkspaceService workspace,
        ILogger<WorkspaceMigrationService> logger)
    {
        _db = db;
        _workspace = workspace;
        _logger = logger;
    }

    /// <summary>
    /// 执行全量迁移。
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(CancellationToken ct = default)
    {
        // 提示清理旧临时目录
        var oldTempDir = Path.Combine(Path.GetTempPath(), "heimdall_repos");
        if (Directory.Exists(oldTempDir))
        {
            _logger.LogWarning("检测到旧仓库缓存目录 {OldDir}，迁移完成后可手动删除以释放磁盘空间", oldTempDir);
        }

        var result = new MigrationResult();
        result.AstVersions = await MigrateAstVersionsAsync(ct);
        result.WikiVersions = await MigrateWikiVersionsAsync(ct);
        result.WikiPages = await MigrateWikiPagesAsync(ct);
        result.TaskArtifacts = await MigrateTaskArtifactsAsync(ct);
        result.LlmCallLogs = await MigrateLlmCallLogsAsync(ct);
        return result;
    }

    private async Task<int> MigrateAstVersionsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var versions = await _db.Queryable<AstVersion>()
            .Where(v => v.ResultJson != null && v.AstDirPath == null)
            .ToListAsync(ct);

        foreach (var version in versions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var astDir = _workspace.GetAstDir(version.Id);
                var filesDir = Path.Combine(astDir, "files");
                Directory.CreateDirectory(filesDir);

                // 从 ResultJson 重建文件
                if (!string.IsNullOrEmpty(version.ResultJson))
                {
                    var fileResults = JsonSerializer.Deserialize<List<object>>(version.ResultJson);
                    if (fileResults != null)
                    {
                        foreach (var fr in fileResults)
                        {
                            var json = JsonSerializer.Serialize(fr);
                            var hash = Convert.ToHexString(
                                System.Security.Cryptography.SHA256.HashData(
                                    System.Text.Encoding.UTF8.GetBytes(json)))[..16];
                            await File.WriteAllTextAsync(
                                Path.Combine(filesDir, $"{hash}.cst"), json, ct);
                        }
                    }
                }

                // manifest.json
                var manifest = JsonSerializer.Serialize(new
                {
                    total_files = version.TotalFiles,
                    total_symbols = version.TotalSymbols,
                    total_call_edges = version.TotalCallEdges,
                    total_chunks = version.TotalChunks
                }, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(
                    Path.Combine(astDir, "manifest.json"), manifest, ct);

                // symbols.json
                if (!string.IsNullOrEmpty(version.SymbolNamesJson))
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(astDir, "symbols.json"), version.SymbolNamesJson, ct);
                }

                version.AstDirPath = astDir;
                await _db.Updateable(version)
                    .UpdateColumns(v => new { v.AstDirPath })
                    .ExecuteCommandAsync(ct);

                migrated++;
                _logger.LogInformation("已迁移 AstVersion {Id}", version.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移 AstVersion {Id} 失败", version.Id);
            }
        }

        _logger.LogInformation("AstVersion 迁移完成: {Count}/{Total}", migrated, versions.Count);
        return migrated;
    }

    private async Task<int> MigrateWikiVersionsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var versions = await _db.Queryable<WikiVersion>()
            .Where(v => v.StructureJson != null && v.StructureFilePath == null)
            .ToListAsync(ct);

        foreach (var version in versions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var wikiDir = _workspace.GetWikiDir(version.Id);
                var structurePath = Path.Combine(wikiDir, "structure.json");
                await _workspace.WriteFileAsync(structurePath, version.StructureJson ?? "{}", ct);

                version.StructureFilePath = structurePath;
                await _db.Updateable(version)
                    .UpdateColumns(v => new { v.StructureFilePath })
                    .ExecuteCommandAsync(ct);

                migrated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移 WikiVersion {Id} 失败", version.Id);
            }
        }

        _logger.LogInformation("WikiVersion 迁移完成: {Count}/{Total}", migrated, versions.Count);
        return migrated;
    }

    private async Task<int> MigrateWikiPagesAsync(CancellationToken ct)
    {
        var migrated = 0;
        var pages = await _db.Queryable<WikiPage>()
            .Where(p => p.ContentMarkdown != null && p.ContentFilePath == null)
            .ToListAsync(ct);

        foreach (var page in pages)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var wikiDir = _workspace.GetWikiDir(page.WikiVersionId);
                var pagesDir = Path.Combine(wikiDir, "pages");
                var slug = ToSlug(page.Title);
                var fileName = $"{page.PageOrder:D4}_{slug}.md";
                var filePath = Path.Combine(pagesDir, fileName);

                await _workspace.WriteFileAsync(filePath, page.ContentMarkdown ?? "", ct);

                page.ContentFilePath = filePath;
                await _db.Updateable(page)
                    .UpdateColumns(p => new { p.ContentFilePath })
                    .ExecuteCommandAsync(ct);

                migrated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移 WikiPage {Id} 失败", page.Id);
            }
        }

        _logger.LogInformation("WikiPage 迁移完成: {Count}/{Total}", migrated, pages.Count);
        return migrated;
    }

    private async Task<int> MigrateTaskArtifactsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var artifacts = await _db.Queryable<TaskArtifact>()
            .Where(a => a.PayloadJson != null && a.PayloadJson != "{}" && a.PayloadFilePath == null)
            .ToListAsync(ct);

        foreach (var artifact in artifacts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var dir = _workspace.GetArtifactDir(artifact.TaskId);
                var filePath = Path.Combine(dir, $"{artifact.ArtifactType}.json");
                await _workspace.WriteFileAsync(filePath, artifact.PayloadJson, ct);

                artifact.PayloadFilePath = filePath;
                await _db.Updateable(artifact)
                    .UpdateColumns(a => new { a.PayloadFilePath })
                    .ExecuteCommandAsync(ct);

                migrated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移 TaskArtifact {Id} 失败", artifact.Id);
            }
        }

        _logger.LogInformation("TaskArtifact 迁移完成: {Count}/{Total}", migrated, artifacts.Count);
        return migrated;
    }

    private async Task<int> MigrateLlmCallLogsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var logs = await _db.Queryable<TaskLlmCallLog>()
            .Where(l => l.LogFilePath == null)
            .ToListAsync(ct);

        // 按 task_id 分组，同一 task 写入同一个 calls.jsonl
        var groups = logs.GroupBy(l => l.TaskId);
        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var logDir = _workspace.GetLogDir(group.Key);
                var logFilePath = Path.Combine(logDir, "calls.jsonl");

                foreach (var log in group.OrderBy(l => l.StepOrder))
                {
                    var entry = JsonSerializer.Serialize(new
                    {
                        step_order = log.StepOrder,
                        call_type = log.CallType,
                        provider = log.Provider,
                        model = log.Model,
                        prompt_tokens = log.PromptTokens,
                        completion_tokens = log.CompletionTokens,
                        total_tokens = log.TotalTokens,
                        latency_ms = log.LatencyMs,
                        is_error = log.IsError,
                        error_message = log.ErrorMessage,
                        tool_call_logs = log.ToolCallLogsJson,
                        request_preview = log.RequestPreview,
                        response_preview = log.ResponsePreview,
                        created_at = log.CreatedAt.ToString("O")
                    });
                    await _workspace.AppendLineAsync(logFilePath, entry, ct);
                }

                // 更新同组所有记录的 log_file_path
                foreach (var log in group)
                {
                    log.LogFilePath = logFilePath;
                }
                await _db.Updateable(group.ToList())
                    .UpdateColumns(l => new { l.LogFilePath })
                    .ExecuteCommandAsync(ct);

                migrated += group.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移 TaskLlmCallLog 组 TaskId={TaskId} 失败", group.Key);
            }
        }

        _logger.LogInformation("TaskLlmCallLog 迁移完成: {Count}/{Total}", migrated, logs.Count);
        return migrated;
    }

    private static string ToSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";
        var slug = System.Text.RegularExpressions.Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9一-鿿]+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-{2,}", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }
}

public class MigrationResult
{
    public int AstVersions { get; set; }
    public int WikiVersions { get; set; }
    public int WikiPages { get; set; }
    public int TaskArtifacts { get; set; }
    public int LlmCallLogs { get; set; }
    public int Total => AstVersions + WikiVersions + WikiPages + TaskArtifacts + LlmCallLogs;
}
