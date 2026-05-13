using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiRepository : IWikiRepository
{
    private readonly AppDbContext _context;

    public WikiRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Wiki?> GetByIdAsync(Guid id)
    {
        return await _context.Wikis
            .Include(w => w.Pages)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Wiki?> GetByRepoBranchLanguageAsync(Guid sourceRepositoryId, string sourceBranch, string language)
    {
        return await _context.Wikis
            .AsNoTracking()
            .Include(w => w.Pages)
            .FirstOrDefaultAsync(w => w.SourceRepositoryId == sourceRepositoryId
                && w.SourceBranch == sourceBranch
                && w.Language == language);
    }

    public async Task<Wiki> AddAsync(Wiki wiki)
    {
        wiki.CreatedAt = DateTime.UtcNow;
        wiki.UpdatedAt = DateTime.UtcNow;
        _context.Wikis.Add(wiki);
        await _context.SaveChangesAsync();
        return wiki;
    }

    public async Task<Wiki> UpdateAsync(Wiki wiki)
    {
        wiki.UpdatedAt = DateTime.UtcNow;
        _context.Wikis.Update(wiki);
        await _context.SaveChangesAsync();
        return wiki;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var wiki = await _context.Wikis.FindAsync(id);
        if (wiki is null) return false;
        _context.Wikis.Remove(wiki);
        await _context.SaveChangesAsync();
        return true;
    }
}
