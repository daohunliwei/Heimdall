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
        var projects = new List<object>();

        foreach (var r in repos)
        {
            // 获取默认 WikiSpace 的版本信息
            var space = await _spaceRepo.GetByRepoLangViewAsync(r.Id, r.DefaultLanguage ?? "zh", "default");
            string? latestWikiVersionId = null;
            string? publishedWikiVersionId = null;

            if (space is not null)
            {
                publishedWikiVersionId = space.PublishedWikiVersionId?.ToString();

                var versions = await _versionRepo.GetBySpaceIdAsync(space.Id);
                var latest = versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
                latestWikiVersionId = latest?.Id.ToString();
            }

            projects.Add(new
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
            });
        }

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
