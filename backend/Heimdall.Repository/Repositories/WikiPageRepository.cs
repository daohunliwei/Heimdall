using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

/// <summary>
/// Wiki 页面仓储实现。V4：已移除旧 WikiId 相关查询，所有页面读写统一通过 WikiVersionId。
/// </summary>
public class WikiPageRepository : IWikiPageRepository
{
    private readonly ISqlSugarClient _db;

    /// <summary>初始化仓储</summary>
    public WikiPageRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 按 WikiVersionId 读取页面集合。
    /// 这是版本化页面读取的唯一入口，用于版本切换、版本比较与缓存回放。
    /// </summary>
    /// <param name="wikiVersionId">Wiki 版本 ID</param>
    /// <returns>页面列表（按 PageOrder 排序）</returns>
    public async Task<List<WikiPage>> GetByWikiVersionIdAsync(Guid wikiVersionId)
    {
        return await _db.Queryable<WikiPage>()
            .Where(p => p.WikiVersionId == wikiVersionId)
            .OrderBy(p => p.PageOrder)
            .ToListAsync();
    }

    /// <summary>新增页面</summary>
    public async Task<WikiPage> AddAsync(WikiPage page)
    {
        page.CreatedAt = DateTime.UtcNow;
        page.UpdatedAt = DateTime.UtcNow;
        await _db.Insertable(page).ExecuteCommandAsync();
        return page;
    }

    /// <summary>批量新增页面</summary>
    public async Task<List<WikiPage>> AddRangeAsync(IEnumerable<WikiPage> pages)
    {
        var pageList = pages.ToList();
        foreach (var page in pageList)
        {
            page.CreatedAt = DateTime.UtcNow;
            page.UpdatedAt = DateTime.UtcNow;
        }
        await _db.Insertable(pageList).ExecuteCommandAsync();
        return pageList;
    }

    /// <summary>更新页面</summary>
    public async Task<WikiPage> UpdateAsync(WikiPage page)
    {
        page.UpdatedAt = DateTime.UtcNow;
        await _db.Updateable(page).ExecuteCommandAsync();
        return page;
    }
}
