using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("admin/repositories")]
[Authorize(Policy = "AdminOnly")]
public class RepositoriesAdminController : ControllerBase
{
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IEmbeddingRepository _embeddingRepo;

    public RepositoriesAdminController(
        IRepositoryConfigRepository repoRepo,
        IEmbeddingRepository embeddingRepo)
    {
        _repoRepo = repoRepo;
        _embeddingRepo = embeddingRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var repos = await _repoRepo.GetAllAsync();
        return Ok(repos.Select(r => new
        {
            id = r.Id.ToString(),
            owner = r.Owner,
            repo_name = r.RepoName,
            repo_type = r.RepoType,
            repo_url = r.RepoUrl,
            default_branch = r.DefaultBranch,
            created_at = r.CreatedAt
        }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _embeddingRepo.DeleteByRepoAsync(id);
        var deleted = await _repoRepo.DeleteAsync(id);
        return deleted ? Ok() : NotFound();
    }

    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id)
    {
        var repo = await _repoRepo.GetByIdAsync(id);
        if (repo is null) return NotFound();

        // 清除嵌入缓存，前端可重新提交生成任务
        await _embeddingRepo.DeleteByRepoAsync(id);
        return Ok(new { status = "cleared" });
    }
}
