using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiPageRelationRepository : BaseRepository<WikiPageRelation>, IWikiPageRelationRepository
{
    public WikiPageRelationRepository(ISqlSugarClient db) : base(db) { }

    public async Task<List<WikiPageRelation>> GetByVersionIdAsync(Guid wikiVersionId)
    {
        return await Context.Queryable<WikiPageRelation>()
            .Where(r => r.WikiVersionId == wikiVersionId)
            .ToListAsync();
    }

    public async Task<List<WikiPageRelation>> GetBySourcePageIdAsync(Guid pageId)
    {
        return await Context.Queryable<WikiPageRelation>()
            .Where(r => r.SourcePageId == pageId)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<WikiPageRelation> relations)
    {
        await Context.Insertable(relations.ToList()).PageSize(1000).ExecuteCommandAsync();
    }

    public async Task<int> DeleteByVersionIdAsync(Guid wikiVersionId)
    {
        return await Context.Deleteable<WikiPageRelation>()
            .Where(r => r.WikiVersionId == wikiVersionId)
            .ExecuteCommandAsync();
    }
}
