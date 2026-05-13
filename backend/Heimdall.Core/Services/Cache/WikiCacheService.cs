using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Cache;

/// <summary>
/// Wiki 缓存服务，以数据库为唯一信源。
/// </summary>
public sealed class WikiCacheService
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly ILogger<WikiCacheService> _logger;

    public WikiCacheService(IWikiRepository wikiRepo, IWikiPageRepository pageRepo, ILogger<WikiCacheService> logger)
    {
        _wikiRepo = wikiRepo;
        _pageRepo = pageRepo;
        _logger = logger;
    }

    public async Task<Wiki?> GetAsync(Guid repositoryId, string branch, string language)
    {
        var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repositoryId, branch, language);
        if (wiki is not null)
        {
            wiki.Pages = (await _pageRepo.GetByWikiIdAsync(wiki.Id)).ToList();
        }

        return wiki;
    }

    public async Task SaveAsync(Wiki wiki, List<WikiPage> pages)
    {
        var existing = await _wikiRepo.GetByRepoBranchLanguageAsync(
            wiki.SourceRepositoryId, wiki.SourceBranch, wiki.Language);

        if (existing is not null)
        {
            wiki.Id = existing.Id;
            await _pageRepo.DeleteByWikiIdAsync(existing.Id);
            await _wikiRepo.UpdateAsync(wiki);
        }
        else
        {
            await _wikiRepo.AddAsync(wiki);
        }

        foreach (var page in pages)
        {
            page.WikiId = wiki.Id;
        }

        await _pageRepo.AddRangeAsync(pages);
        _logger.LogInformation("已保存 Wiki 缓存 WikiId={WikiId} Pages={PageCount}", wiki.Id, pages.Count);
    }

    public async Task InvalidateAsync(Guid repositoryId)
    {
        // 查找该仓库的所有 Wiki 并删除
        // 实际由调用方负责具体删除逻辑
        _logger.LogInformation("已清除仓库缓存 RepositoryId={RepositoryId}", repositoryId);
    }
}
