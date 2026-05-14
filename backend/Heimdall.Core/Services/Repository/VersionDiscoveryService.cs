using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Core.Services.Repository;

public class VersionDiscoveryService : IVersionDiscoveryService
{
    private readonly IRepositoryVersionRepository _versionRepo;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly ILogger<VersionDiscoveryService> _logger;

    public VersionDiscoveryService(
        IRepositoryVersionRepository versionRepo,
        IRepositoryConfigRepository repoRepo,
        ILogger<VersionDiscoveryService> logger)
    {
        _versionRepo = versionRepo;
        _repoRepo = repoRepo;
        _logger = logger;
    }

    public async Task<RepositoryVersion> DiscoverRepositoryVersionAsync(Guid repositoryId, string branch, CancellationToken cancellationToken = default)
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null)
            throw new InvalidOperationException($"仓库不存在：{repositoryId}");

        string commitSha;
        DateTime commitTime;
        string? commitAuthor = null;
        string? commitMessage = null;

        try
        {
            (commitSha, commitTime, commitAuthor, commitMessage) = await ResolveRemoteHeadAsync(repo, branch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法获取远端 HEAD 信息，使用占位值 RepositoryId={RepoId} Branch={Branch}", repositoryId, branch);
            commitSha = "unknown";
            commitTime = DateTime.UtcNow;
        }

        // 查找是否已存在该快照
        var existing = await _versionRepo.GetByRepoBranchCommitAsync(repositoryId, branch, commitSha);
        if (existing is not null)
        {
            _logger.LogInformation("仓库版本已存在 VersionId={VersionId} CommitSha={Sha}", existing.Id, commitSha);
            return existing;
        }

        // 将旧的该分支版本标记为非最新
        var allVersions = await _versionRepo.GetByRepositoryIdAsync(repositoryId);
        var oldLatest = allVersions.Where(v => v.BranchName == branch && v.IsLatestOnBranch).ToList();

        foreach (var ov in oldLatest)
        {
            ov.IsLatestOnBranch = false;
            if (ov.SourceStatus == "active")
                ov.SourceStatus = "superseded";
        }

        if (oldLatest.Count > 0)
            await _versionRepo.UpdateRangeAsync(oldLatest);

        // 创建新版本
        var version = new RepositoryVersion
        {
            RepositoryId = repositoryId,
            BranchName = branch,
            CommitSha = commitSha,
            CommitTime = commitTime,
            CommitAuthor = commitAuthor,
            CommitMessage = commitMessage,
            IsLatestOnBranch = true,
            SourceStatus = "active",
            VersionSourceConfidence = commitSha == "unknown" ? "unknown" : "exact"
        };

        var created = await _versionRepo.AddAsync(version);

        _logger.LogInformation("新建仓库版本 VersionId={VersionId} CommitSha={Sha} Branch={Branch}",
            created.Id, commitSha, branch);

        return created;
    }

    public async Task<RepositoryVersion?> GetLatestVersionAsync(Guid repositoryId, string branch, CancellationToken cancellationToken = default)
    {
        return await _versionRepo.GetLatestByRepoBranchAsync(repositoryId, branch);
    }

    public async Task<List<RepositoryVersion>> GetVersionsAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        return await _versionRepo.GetByRepositoryIdAsync(repositoryId);
    }

    private async Task<(string commitSha, DateTime commitTime, string? author, string? message)> ResolveRemoteHeadAsync(
        RepositoryEntity repo, string branch)
    {
        if (repo.ProviderType == "local" && !string.IsNullOrWhiteSpace(repo.CloneUrl))
            return await ResolveLocalGitHeadAsync(repo.CloneUrl, branch);

        if (!string.IsNullOrWhiteSpace(repo.CloneUrl))
            return await ResolveRemoteGitHeadAsync(repo.CloneUrl, branch);

        throw new InvalidOperationException("无法获取仓库远端信息：缺少 clone_url");
    }

    private static async Task<(string commitSha, DateTime commitTime, string? author, string? message)> ResolveLocalGitHeadAsync(
        string repoPath, string branch)
    {
        var sha = await RunGitCommandAsync(repoPath, $"rev-parse {branch}");
        if (string.IsNullOrWhiteSpace(sha))
            sha = await RunGitCommandAsync(repoPath, "rev-parse HEAD");

        if (string.IsNullOrWhiteSpace(sha))
            throw new InvalidOperationException("无法获取 git HEAD 提交哈希");

        var logOutput = await RunGitCommandAsync(repoPath, $"log -1 --format=%ct||%an||%s {sha}");
        var parts = logOutput?.Split("||") ?? [];
        var commitTime = parts.Length > 0 && long.TryParse(parts[0], out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime
            : DateTime.UtcNow;
        var author = parts.Length > 1 ? parts[1] : null;
        var message = parts.Length > 2 ? parts[2] : null;

        return (sha.Trim(), commitTime, author, message);
    }

    private static async Task<(string commitSha, DateTime commitTime, string? author, string? message)> ResolveRemoteGitHeadAsync(
        string cloneUrl, string branch)
    {
        var output = await RunGitCommandAsync(null, $"ls-remote --heads {cloneUrl} refs/heads/{branch}");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException($"无法获取远端分支信息：{cloneUrl} / {branch}");

        var sha = output.Split('\t')[0].Trim();
        return (sha, DateTime.UtcNow, null, null);
    }

    private static async Task<string> RunGitCommandAsync(string? workingDir, string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrWhiteSpace(workingDir))
                psi.WorkingDirectory = workingDir;

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null) return string.Empty;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
