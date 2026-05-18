using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiPageRelationRepository : IWikiPageRelationRepository
{
    private readonly AppDbContext _context;
    public WikiPageRelationRepository(AppDbContext context) => _context = context;

    public async Task<List<WikiPageRelation>> GetByVersionIdAsync(Guid wikiVersionId)
    {
        return await _context.WikiPageRelations
            .Where(r => r.WikiVersionId == wikiVersionId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<WikiPageRelation>> GetBySourcePageIdAsync(Guid pageId)
    {
        return await _context.WikiPageRelations
            .Where(r => r.SourcePageId == pageId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<WikiPageRelation> relations)
    {
        _context.WikiPageRelations.AddRange(relations);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteByVersionIdAsync(Guid wikiVersionId)
    {
        return await _context.WikiPageRelations
            .Where(r => r.WikiVersionId == wikiVersionId)
            .ExecuteDeleteAsync();
    }
}
