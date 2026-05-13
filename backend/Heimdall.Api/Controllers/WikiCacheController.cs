using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api")]
public class WikiCacheController : ControllerBase
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly IRepositoryConfigRepository _repoRepo;

    public WikiCacheController(
        IWikiRepository wikiRepo,
        IWikiPageRepository pageRepo,
        IRepositoryConfigRepository repoRepo)
    {
        _wikiRepo = wikiRepo;
        _pageRepo = pageRepo;
        _repoRepo = repoRepo;
    }

    /// <summary>
    /// DELETE /api/wiki_cache?owner=&repo=&repo_type=&language= — 删除指定仓库的 Wiki 缓存。
    /// </summary>
    [HttpDelete("wiki_cache")]
    public async Task<IActionResult> DeleteWikiCache(
        [FromQuery] string owner,
        [FromQuery] string repo,
        [FromQuery] string repo_type,
        [FromQuery] string language)
    {
        var repoEntity = await _repoRepo.GetByOwnerRepoTypeAsync(owner, repo, repo_type);
        if (repoEntity is null)
            return NotFound(new { error = "仓库不存在" });

        var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoEntity.Id, "main", language);
        if (wiki is not null)
        {
            await _pageRepo.DeleteByWikiIdAsync(wiki.Id);
            await _wikiRepo.DeleteAsync(wiki.Id);
        }

        return Ok(new { message = "缓存已清除" });
    }
}
