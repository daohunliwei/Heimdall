using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiPageRelationRepository : IWikiPageRelationRepository
{
    private readonly ISqlSugarClient _db;
    public WikiPageRelationRepository(ISqlSugarClient db) => _db = db;

    public async Task<List<WikiPageRelation>> GetByVersionIdAsync(Guid wikiVersionId)
    {
        return await _db.Queryable<WikiPageRelation>()
            .Where(r => r.WikiVersionId == wikiVersionId)
            .ToListAsync();
    }

    public async Task<List<WikiPageRelation>> GetBySourcePageIdAsync(Guid pageId)
    {
        return await _db.Queryable<WikiPageRelation>()
            .Where(r => r.SourcePageId == pageId)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<WikiPageRelation> relations)
    {
        await _db.Insertable(relations.ToList()).ExecuteCommandAsync();
    }

    public async Task<int> DeleteByVersionIdAsync(Guid wikiVersionId)
    {
        return await _db.Deleteable<WikiPageRelation>()
            .Where(r => r.WikiVersionId == wikiVersionId)
            .ExecuteCommandAsync();
    }
}
