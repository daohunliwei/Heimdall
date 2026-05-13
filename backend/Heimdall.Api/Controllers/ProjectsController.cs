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
    /// </summary>
    [HttpGet("processed_projects")]
    public async Task<IActionResult> GetProcessedProjects()
    {
        var repos = await _repoRepo.GetAllAsync();
        var projects = repos.Select(r => new
        {
            id = r.Id.ToString(),
            owner = r.Owner,
            repo = r.RepoName,
            name = $"{r.Owner}/{r.RepoName}",
            repo_type = r.RepoType,
            submittedAt = ((DateTimeOffset)r.CreatedAt).ToUnixTimeMilliseconds(),
            language = r.DefaultLanguage
        });

        return Ok(projects);
    }
}
