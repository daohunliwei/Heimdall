using System.Text.Json.Serialization;
using Heimdall.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId:guid}/versions")]
public class RepositoryVersionsController : ControllerBase
{
    private readonly IVersionDiscoveryService _versionDiscovery;
    private readonly IRefreshOrchestrationService _refreshOrch;

    public RepositoryVersionsController(
        IVersionDiscoveryService versionDiscovery,
        IRefreshOrchestrationService refreshOrch)
    {
        _versionDiscovery = versionDiscovery;
        _refreshOrch = refreshOrch;
    }

    /// <summary>GET /api/repositories/{repositoryId}/versions — 获取仓库的所有已知版本</summary>
    [HttpGet]
    public async Task<IActionResult> GetVersions(Guid repositoryId)
    {
        var versions = await _versionDiscovery.GetVersionsAsync(repositoryId);
        var result = versions.Select(v => new
        {
            repository_version_id = v.Id.ToString(),
            repository_id = v.RepositoryId.ToString(),
            branch_name = v.BranchName,
            commit_sha = v.CommitSha,
            commit_time = v.CommitTime,
            commit_author = v.CommitAuthor,
            commit_message = v.CommitMessage,
            is_latest_on_branch = v.IsLatestOnBranch,
            source_status = v.SourceStatus,
            version_source_confidence = v.VersionSourceConfidence,
            created_at = v.CreatedAt
        });
        return Ok(result);
    }

    /// <summary>GET /api/repositories/{repositoryId}/versions/{versionId} — 获取指定版本详情</summary>
    [HttpGet("{versionId:guid}")]
    public async Task<IActionResult> GetVersion(Guid repositoryId, Guid versionId)
    {
        var versions = await _versionDiscovery.GetVersionsAsync(repositoryId);
        var v = versions.FirstOrDefault(x => x.Id == versionId);
        if (v is null) return NotFound(new { error = "版本不存在" });

        return Ok(new
        {
            repository_version_id = v.Id.ToString(),
            repository_id = v.RepositoryId.ToString(),
            branch_name = v.BranchName,
            commit_sha = v.CommitSha,
            commit_time = v.CommitTime,
            commit_author = v.CommitAuthor,
            commit_message = v.CommitMessage,
            is_latest_on_branch = v.IsLatestOnBranch,
            source_status = v.SourceStatus,
            version_source_confidence = v.VersionSourceConfidence,
            created_at = v.CreatedAt
        });
    }

    /// <summary>POST /api/repositories/{repositoryId}/versions/discover — 主动发现新版本</summary>
    [HttpPost("discover")]
    public async Task<IActionResult> Discover(Guid repositoryId, [FromBody] DiscoverRequest request)
    {
        var branch = !string.IsNullOrWhiteSpace(request.Branch) ? request.Branch : "main";
        var version = await _versionDiscovery.DiscoverRepositoryVersionAsync(repositoryId, branch);
        return Ok(new
        {
            repository_version_id = version.Id.ToString(),
            commit_sha = version.CommitSha,
            is_new = version.CreatedAt > DateTime.UtcNow.AddSeconds(-5),
            branch_name = version.BranchName
        });
    }

    /// <summary>GET /api/repositories/{repositoryId}/versions/latest — 获取最新版本</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(Guid repositoryId, [FromQuery] string branch = "main")
    {
        var version = await _versionDiscovery.GetLatestVersionAsync(repositoryId, branch);
        if (version is null) return NotFound(new { error = "未找到版本" });

        return Ok(new
        {
            repository_version_id = version.Id.ToString(),
            commit_sha = version.CommitSha,
            branch_name = version.BranchName,
            commit_time = version.CommitTime,
            is_latest_on_branch = version.IsLatestOnBranch
        });
    }
}

public class DiscoverRequest
{
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";
}
