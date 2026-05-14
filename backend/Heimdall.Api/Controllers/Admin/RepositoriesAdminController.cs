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
    private readonly ICodeEmbeddingRepository _codeEmbeddingRepo;
    private readonly IWikiEmbeddingRepository _wikiEmbeddingRepo;
    private readonly IRepositoryVersionRepository _versionRepo;
    private readonly IWikiVersionRepository _wikiVersionRepo;

    public RepositoriesAdminController(
        IRepositoryConfigRepository repoRepo,
        ICodeEmbeddingRepository codeEmbeddingRepo,
        IWikiEmbeddingRepository wikiEmbeddingRepo,
        IRepositoryVersionRepository versionRepo,
        IWikiVersionRepository wikiVersionRepo)
    {
        _repoRepo = repoRepo;
        _codeEmbeddingRepo = codeEmbeddingRepo;
        _wikiEmbeddingRepo = wikiEmbeddingRepo;
        _versionRepo = versionRepo;
        _wikiVersionRepo = wikiVersionRepo;
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
        // 清理向量数据
        var versions = await _versionRepo.GetByRepositoryIdAsync(id);
        foreach (var rv in versions)
            await _codeEmbeddingRepo.DeleteByVersionIdAsync(rv.Id);

        var deleted = await _repoRepo.DeleteAsync(id);
        return deleted ? Ok() : NotFound();
    }

    [HttpPost("{id}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id)
    {
        var repo = await _repoRepo.GetByIdAsync(id);
        if (repo is null) return NotFound();

        // 清除向量缓存
        var versions = await _versionRepo.GetByRepositoryIdAsync(id);
        foreach (var rv in versions)
            await _codeEmbeddingRepo.DeleteByVersionIdAsync(rv.Id);

        return Ok(new { status = "cleared" });
    }
}
