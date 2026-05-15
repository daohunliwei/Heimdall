using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
public class WikiCacheController : ControllerBase
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiPageRelationRepository _relationRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IWikiVersionRepository _versionRepo;

    public WikiCacheController(
        IWikiRepository wikiRepo,
        IWikiPageRepository pageRepo,
        IRepositoryConfigRepository repoRepo,
        IWikiPageRelationRepository relationRepo,
        IWikiSpaceRepository spaceRepo,
        IWikiVersionRepository versionRepo)
    {
        _wikiRepo = wikiRepo;
        _pageRepo = pageRepo;
        _repoRepo = repoRepo;
        _relationRepo = relationRepo;
        _spaceRepo = spaceRepo;
        _versionRepo = versionRepo;
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

        var versionedSnapshot = await LoadVersionedSnapshotAsync(repoEntity.Id, language);
        if (versionedSnapshot is not null)
            return Ok(await BuildVersionedWikiResponseAsync(repoEntity, versionedSnapshot.Value.Space, versionedSnapshot.Value.Version, versionedSnapshot.Value.Pages));

        var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoEntity.Id, repoEntity.DefaultBranch ?? "main", language);
        if (wiki is null)
            return NotFound(new { error = "Wiki 缓存不存在" });

        return Ok(await BuildLegacyWikiResponseAsync(repoEntity, wiki));
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

    /// <summary>
    /// 读取版本化 Wiki 快照。
    /// 优先返回“已发布版本”，若尚未发布则回退到当前空间中的最新版本。
    /// </summary>
    private async Task<(Core.Entities.WikiSpace Space, Core.Entities.WikiVersion Version, List<Core.Entities.WikiPage> Pages)?> LoadVersionedSnapshotAsync(Guid repositoryId, string language)
    {
        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, "default");
        if (space is null)
            return null;

        Core.Entities.WikiVersion? version = null;
        if (space.PublishedWikiVersionId.HasValue)
            version = await _versionRepo.GetByIdAsync(space.PublishedWikiVersionId.Value);

        if (version is null)
        {
            var versions = await _versionRepo.GetBySpaceIdAsync(space.Id);
            version = versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
        }

        if (version is null)
            return null;

        var pages = await _pageRepo.GetByWikiVersionIdAsync(version.Id);
        return (space, version, pages);
    }

    /// <summary>
    /// 构建版本化 Wiki 缓存响应。
    /// 返回结构保持与旧前端兼容，但页面主键统一切换为真正的页面标识与版本标识。
    /// </summary>
    private async Task<object> BuildVersionedWikiResponseAsync(
        Core.Entities.Repository repoEntity,
        Core.Entities.WikiSpace space,
        Core.Entities.WikiVersion version,
        List<Core.Entities.WikiPage> pages)
    {
        var pageGuidToTitle = pages.ToDictionary(p => p.Id, p => p.Title);
        var pageRelations = new Dictionary<Guid, List<string>>();

        if (pages.Count > 0)
        {
            var pageGuids = pages.Select(p => p.Id).ToHashSet();
            var allRelations = new List<Core.Entities.WikiPageRelation>();

            foreach (var pageId in pageGuids)
            {
                var rels = await _relationRepo.GetBySourcePageIdAsync(pageId);
                allRelations.AddRange(rels);
            }

            foreach (var rel in allRelations)
            {
                if (!pageRelations.ContainsKey(rel.SourcePageId))
                    pageRelations[rel.SourcePageId] = new List<string>();

                if (pageGuidToTitle.ContainsKey(rel.TargetPageId))
                    pageRelations[rel.SourcePageId].Add(rel.TargetPageId.ToString());
            }
        }

        var generatedPages = new Dictionary<string, object>();
        foreach (var p in pages)
        {
            generatedPages[p.Id.ToString()] = new
            {
                id = p.Id.ToString(),
                title = p.Title,
                content = p.ContentMarkdown ?? "",
                importance = p.Importance,
                filePaths = p.FilePaths ?? Array.Empty<string>(),
                pageOrder = p.PageOrder,
                relatedPages = pageRelations.TryGetValue(p.Id, out var related)
                    ? (IReadOnlyList<string>)related
                    : Array.Empty<string>()
            };
        }

        return new
        {
            repository_id = repoEntity.Id.ToString(),
            wiki_version_id = version.Id.ToString(),
            published_wiki_version_id = space.PublishedWikiVersionId?.ToString(),
            latest_wiki_version_id = version.Id.ToString(),
            repo = new { owner = repoEntity.Owner, repo = repoEntity.RepoName, type = repoEntity.RepoType, url = repoEntity.RepoUrl },
            wikiStructure = new
            {
                id = version.Id.ToString(),
                title = space.Title,
                description = space.Description ?? version.SummaryMarkdown ?? "",
                pages = pages.Select(p => new
                {
                    id = p.Id.ToString(),
                    title = p.Title,
                    description = "",
                    importance = p.Importance,
                    filePaths = p.FilePaths ?? Array.Empty<string>(),
                    relatedPages = pageRelations.TryGetValue(p.Id, out var related)
                        ? (IReadOnlyList<string>)related
                        : Array.Empty<string>()
                }).ToList(),
                sections = new List<object>(),
                rootSections = new List<string>()
            },
            generatedPages = generatedPages,
            provider = "ollama",
            model = "gemma4:e2b"
        };
    }

    /// <summary>
    /// 构建旧 Wiki 主表兼容响应。
    /// 当仓库尚未进入版本化模型时，仍然允许前端读取历史缓存内容。
    /// </summary>
    private async Task<object> BuildLegacyWikiResponseAsync(Core.Entities.Repository repoEntity, Core.Entities.Wiki wiki)
    {
        var pages = await _pageRepo.GetByWikiIdAsync(wiki.Id);
        var generatedPages = pages.ToDictionary(
            p => p.Id.ToString(),
            p => (object)new
            {
                id = p.Id.ToString(),
                title = p.Title,
                content = p.ContentMarkdown ?? "",
                importance = p.Importance,
                filePaths = p.FilePaths ?? Array.Empty<string>(),
                pageOrder = p.PageOrder
            });

        return new
        {
            repository_id = repoEntity.Id.ToString(),
            repo = new { owner = repoEntity.Owner, repo = repoEntity.RepoName, type = repoEntity.RepoType, url = repoEntity.RepoUrl },
            wikiStructure = new
            {
                id = wiki.Id.ToString(),
                title = wiki.Title,
                description = wiki.Description ?? "",
                pages = pages.Select(p => new
                {
                    id = p.Id.ToString(),
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
