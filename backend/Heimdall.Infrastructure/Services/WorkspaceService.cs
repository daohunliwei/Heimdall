using System.Text.Json;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Workspace 统一入口——封装根路径、目录初始化、路径解析与文件读写。
/// 全系统统一用此服务解析路径，避免各处硬编码拼接。
/// </summary>
public sealed class WorkspaceService
{
    private readonly ILogger<WorkspaceService> _logger;
    private readonly WorkspaceConfig _config;

    public string RootPath => _config.RootPath;

    private static readonly string[] TopLevelDirs =
        ["repos", "ast", "wiki", "artifacts", "logs", "cache"];

    public WorkspaceService(WorkspaceConfig config, ILogger<WorkspaceService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 确保根目录和所有顶层子目录存在。
    /// </summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootPath);
        foreach (var dir in TopLevelDirs)
        {
            Directory.CreateDirectory(Path.Combine(RootPath, dir));
        }
        _logger.LogInformation("Workspace 目录已就绪: {RootPath}", RootPath);
    }

    /// <summary>
    /// 仓库克隆路径 → repos/{owner}_{repo}/
    /// </summary>
    public string GetRepoPath(string owner, string repo) =>
        Path.Combine(RootPath, "repos", $"{owner}_{repo}");

    /// <summary>
    /// AST 解析结果目录 → ast/{versionId[:8]}/
    /// </summary>
    public string GetAstDir(Guid astVersionId) =>
        Path.Combine(RootPath, "ast", GuidPrefix(astVersionId));

    /// <summary>
    /// Wiki 版本目录 → wiki/{versionId[:8]}/
    /// </summary>
    public string GetWikiDir(Guid wikiVersionId) =>
        Path.Combine(RootPath, "wiki", GuidPrefix(wikiVersionId));

    /// <summary>
    /// 任务工件目录 → artifacts/{taskId[:8]}/
    /// </summary>
    public string GetArtifactDir(Guid taskId) =>
        Path.Combine(RootPath, "artifacts", GuidPrefix(taskId));

    /// <summary>
    /// LLM 日志目录 → logs/{taskId[:8]}/
    /// </summary>
    public string GetLogDir(Guid taskId) =>
        Path.Combine(RootPath, "logs", GuidPrefix(taskId));

    /// <summary>
    /// 缓存目录 → cache/{key}/
    /// </summary>
    public string GetCacheDir(string key) =>
        Path.Combine(RootPath, "cache", key);

    /// <summary>
    /// 文件缺失即重新生成的标准模式。
    /// 若文件存在则直接读回；若不存在则调用 regenerate 生成后写入并返回。
    /// </summary>
    public async Task<string> ReadOrRegenerateAsync(
        string filePath,
        Func<Task<string>> regenerate,
        CancellationToken ct = default)
    {
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath, ct);
        }

        _logger.LogInformation("Workspace 文件缺失，触发重新生成: {Path}", filePath);
        var content = await regenerate();
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(filePath, content, ct);
        return content;
    }

    /// <summary>
    /// 文件缺失即重新生成的标准模式（带 JSON 反序列化）。
    /// 若文件存在则直接读回并反序列化；若不存在则调用 regenerate 生成后写入、返回。
    /// </summary>
    public async Task<T> ReadOrRegenerateJsonAsync<T>(
        string filePath,
        Func<Task<T>> regenerate,
        JsonSerializerOptions? options = null,
        CancellationToken ct = default)
    {
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<T>(json, options ?? JsonSerializerOptions.Default)
                ?? await regenerate();
        }

        _logger.LogInformation("Workspace 文件缺失，触发重新生成: {Path}", filePath);
        var result = await regenerate();
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var content = JsonSerializer.Serialize(result, options ?? JsonSerializerOptions.Default);
        await File.WriteAllTextAsync(filePath, content, ct);
        return result;
    }

    /// <summary>
    /// Workspace 文件优先读取 + DB TEXT 列回退模式。
    /// 若 workspace 文件存在则读回；否则尝试从 dbFallback 获取内容并补写 workspace 文件。
    /// </summary>
    public async Task<string> ReadWithFallbackAsync(
        string filePath,
        Func<Task<string?>> dbFallback,
        CancellationToken ct = default)
    {
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath, ct);
        }

        var content = await dbFallback();
        if (!string.IsNullOrEmpty(content))
        {
            await WriteFileAsync(filePath, content, ct);
            return content;
        }

        return string.Empty;
    }

    /// <summary>
    /// 写入文件并自动创建父目录。
    /// </summary>
    public async Task WriteFileAsync(string filePath, string content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(filePath, content, ct);
    }

    /// <summary>
    /// 追加一行到文件，自动创建父目录。
    /// </summary>
    public async Task AppendLineAsync(string filePath, string line, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.AppendAllTextAsync(filePath, line + Environment.NewLine, ct);
    }

    /// <summary>
    /// Guid 前 8 位十六进制。
    /// </summary>
    private static string GuidPrefix(Guid id) => id.ToString("N")[..8];
}
