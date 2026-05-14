using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
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
    /// GET /api/repositories/{repositoryId}/wiki — 按 repositoryId 读取 Wiki
    /// </summary>
    [HttpGet("api/repositories/{repositoryId:guid}/wiki")]
    public async Task<IActionResult> GetWikiByRepositoryId(Guid repositoryId, [FromQuery] string language = "zh")
    {
        var repoEntity = await _repoRepo.GetByIdAsync(repositoryId);
        if (repoEntity is null)
            return NotFound(new { error = "仓库不存在" });

        var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoEntity.Id, "main", language);
        if (wiki is null)
            return NotFound(new { error = "Wiki 缓存不存在" });

        return Ok(BuildWikiResponse(repoEntity, wiki));
    }

    /// <summary>
    /// DELETE /api/repositories/{repositoryId}/wiki — 按 repositoryId 删除 Wiki
    /// </summary>
    [HttpDelete("api/repositories/{repositoryId:guid}/wiki")]
    public async Task<IActionResult> DeleteWikiByRepositoryId(Guid repositoryId, [FromQuery] string language = "zh")
    {
        var repoEntity = await _repoRepo.GetByIdAsync(repositoryId);
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

    // ===== 旧接口兼容（双轨期保留） =====

    /// <summary>
    /// GET /api/wiki_cache?owner=&repo=&repo_type=&language= — 旧接口，获取缓存的 Wiki 数据。
    /// </summary>
    [HttpGet("api/wiki_cache")]
    public async Task<IActionResult> GetWikiCache(
        [FromQuery] string owner,
        [FromQuery] string repo,
        [FromQuery] string repo_type,
        [FromQuery] string language)
    {
        var repoEntity = await _repoRepo.GetByOwnerRepoTypeAsync(owner, repo, repo_type)
            ?? await _repoRepo.GetByOwnerRepoAnyTypeAsync(owner, repo);

        if (repoEntity is null)
            return NotFound(new { error = "仓库不存在" });

        var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoEntity.Id, "main", language);
        if (wiki is null)
            return NotFound(new { error = "Wiki 缓存不存在" });

        return Ok(BuildWikiResponse(repoEntity, wiki));
    }

    /// <summary>
    /// DELETE /api/wiki_cache?owner=&repo=&repo_type=&language= — 旧接口，删除指定仓库的 Wiki 缓存。
    /// </summary>
    [HttpDelete("api/wiki_cache")]
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

    private async Task<object> BuildWikiResponse(Core.Entities.Repository repoEntity, Core.Entities.Wiki wiki)
    {
        var pages = await _pageRepo.GetByWikiIdAsync(wiki.Id);
        var generatedPages = new Dictionary<string, object>();
        foreach (var p in pages)
        {
            generatedPages[p.Title] = new
            {
                id = p.Id.ToString(),
                title = p.Title,
                content = p.ContentMarkdown ?? "",
                importance = p.Importance,
                filePaths = p.FilePaths ?? Array.Empty<string>(),
                pageOrder = p.PageOrder
            };
        }

        return new
        {
            repository_id = repoEntity.Id.ToString(),
            repo = new { owner = repoEntity.Owner, repo = repoEntity.RepoName, type = repoEntity.RepoType, url = repoEntity.RepoUrl },
            wikiStructure = new
            {
                id = "wiki",
                title = wiki.Title,
                description = wiki.Description ?? "",
                pages = pages.Select(p => new
                {
                    id = p.Title,
                    title = p.Title,
                    description = "",
                    importance = p.Importance,
                    filePaths = p.FilePaths ?? Array.Empty<string>(),
                    relatedPages = Array.Empty<string>()
                }).ToList(),
                sections = new List<object>(),
                rootSections = new List<string>()
            },
            generatedPages = generatedPages,
            provider = "ollama",
            model = "gemma4:e2b"
        };
    }
}
