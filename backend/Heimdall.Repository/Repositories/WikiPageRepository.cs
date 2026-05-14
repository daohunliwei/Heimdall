using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiPageRepository : IWikiPageRepository
{
    private readonly AppDbContext _context;

    public WikiPageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<WikiPage>> GetByWikiIdAsync(Guid wikiId)
    {
        return await _context.WikiPages
            .AsNoTracking()
            .Where(p => p.WikiId == wikiId)
            .OrderBy(p => p.PageOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 按 WikiVersion 直接读取页面集合。
    /// 该查询用于版本切换、版本比较以及缓存回放等版本化读取场景。
    /// </summary>
    public async Task<List<WikiPage>> GetByWikiVersionIdAsync(Guid wikiVersionId)
    {
        return await _context.WikiPages
            .AsNoTracking()
            .Where(p => p.WikiVersionId == wikiVersionId)
            .OrderBy(p => p.PageOrder)
            .ToListAsync();
    }

    public async Task<WikiPage> AddAsync(WikiPage page)
    {
        page.CreatedAt = DateTime.UtcNow;
        page.UpdatedAt = DateTime.UtcNow;
        _context.WikiPages.Add(page);
        await _context.SaveChangesAsync();
        return page;
    }

    public async Task<List<WikiPage>> AddRangeAsync(IEnumerable<WikiPage> pages)
    {
        var pageList = pages.ToList();
        foreach (var page in pageList)
        {
            page.CreatedAt = DateTime.UtcNow;
            page.UpdatedAt = DateTime.UtcNow;
        }
        _context.WikiPages.AddRange(pageList);
        await _context.SaveChangesAsync();
        return pageList;
    }

    public async Task<WikiPage> UpdateAsync(WikiPage page)
    {
        page.UpdatedAt = DateTime.UtcNow;
        _context.WikiPages.Update(page);
        await _context.SaveChangesAsync();
        return page;
    }

    public async Task<bool> DeleteByWikiIdAsync(Guid wikiId)
    {
        var pages = await _context.WikiPages
            .Where(p => p.WikiId == wikiId)
            .ToListAsync();

        if (pages.Count == 0) return false;

        _context.WikiPages.RemoveRange(pages);
        await _context.SaveChangesAsync();
        return true;
    }
}
