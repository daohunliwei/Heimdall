using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api")]
public class ProjectsController : ControllerBase
{
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IWikiVersionRepository _versionRepo;

    public ProjectsController(
        IRepositoryConfigRepository repoRepo,
        IWikiSpaceRepository spaceRepo,
        IWikiVersionRepository versionRepo)
    {
        _repoRepo = repoRepo;
        _spaceRepo = spaceRepo;
        _versionRepo = versionRepo;
    }

    /// <summary>
    /// GET /api/processed_projects — 已处理的项目列表（供前端首页展示）。
    /// 返回字段统一使用 repository_id 作为主标识。
    /// </summary>
    [HttpGet("processed_projects")]
    public async Task<IActionResult> GetProcessedProjects()
    {
        var repos = await _repoRepo.GetAllAsync();
        var repoIds = repos.Select(r => r.Id).ToList();

        // 批量加载所有仓库的 WikiSpace（一次查询）
        var spaces = await _spaceRepo.GetByRepoIdsAsync(repoIds);
        var spaceByRepoId = spaces
            .GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.First());

        // 批量加载所有空间的最新版本（一次查询）
        var spaceIds = spaces.Select(s => s.Id).ToList();
        var allVersions = await _versionRepo.GetBySpaceIdsAsync(spaceIds);
        var versionBySpaceId = allVersions
            .GroupBy(v => v.WikiSpaceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNo).First());

        var projects = repos.Select(r =>
        {
            spaceByRepoId.TryGetValue(r.Id, out var space);
            var latestWikiVersionId = space is not null && versionBySpaceId.TryGetValue(space.Id, out var v) ? v.Id.ToString() : null;
            var publishedWikiVersionId = space?.PublishedWikiVersionId?.ToString();
            return (object)new
            {
                repository_id = r.Id.ToString(),
                id = r.Id.ToString(),
                owner = r.Owner,
                repo = r.RepoName,
                name = r.DisplayName,
                display_name = r.DisplayName,
                repo_type = r.RepoType,
                submittedAt = ((DateTimeOffset)r.CreatedAt).ToUnixTimeMilliseconds(),
                language = r.DefaultLanguage,
                default_branch = r.DefaultBranch,
                latest_wiki_version_id = latestWikiVersionId,
                published_wiki_version_id = publishedWikiVersionId
            };
        }).ToList();

        return Ok(projects);
    }

    /// <summary>
    /// DELETE /api/processed_projects/{repositoryId} — 基于 repositoryId 删除项目
    /// </summary>
    [HttpDelete("processed_projects/{repositoryId:guid}")]
    public async Task<IActionResult> DeleteProject(Guid repositoryId)
    {
        var deleted = await _repoRepo.DeleteAsync(repositoryId);
        if (!deleted) return NotFound(new { error = "仓库不存在" });
        return Ok(new { message = "项目已删除" });
    }
}
