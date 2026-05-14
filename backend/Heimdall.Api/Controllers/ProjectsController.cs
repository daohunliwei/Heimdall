using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api")]
public class ProjectsController : ControllerBase
{
    private readonly IRepositoryConfigRepository _repoRepo;

    public ProjectsController(IRepositoryConfigRepository repoRepo)
    {
        _repoRepo = repoRepo;
    }

    /// <summary>
    /// GET /api/processed_projects — 已处理的项目列表（供前端首页展示）。
    /// 返回字段统一使用 repository_id 作为主标识。
    /// </summary>
    [HttpGet("processed_projects")]
    public async Task<IActionResult> GetProcessedProjects()
    {
        var repos = await _repoRepo.GetAllAsync();
        var projects = repos.Select(r => new
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
            default_branch = r.DefaultBranch
        });

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
