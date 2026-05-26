using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiVersionRepository : BaseRepository<WikiVersion>, IWikiVersionRepository
{
    public WikiVersionRepository(ISqlSugarClient db) : base(db) { }

    public async Task<WikiVersion?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<WikiVersion>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<List<WikiVersion>> GetBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await Context.Queryable<WikiVersion>()
            .Where(v => v.WikiSpaceId == wikiSpaceId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<WikiVersion>> GetBySpaceIdsAsync(IEnumerable<Guid> spaceIds)
    {
        var ids = spaceIds.ToList();
        if (ids.Count == 0) return new List<WikiVersion>();
        return await Context.Queryable<WikiVersion>()
            .Where(v => ids.Contains(v.WikiSpaceId))
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync();
    }

    public async Task<int> CountBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await Context.Queryable<WikiVersion>().CountAsync(v => v.WikiSpaceId == wikiSpaceId);
    }

    public async Task<WikiVersion> AddAsync(WikiVersion version)
    {
        await Context.Insertable(version).ExecuteCommandAsync();
        return version;
    }

    public async Task<WikiVersion> UpdateAsync(WikiVersion version)
    {
        await Context.Updateable(version).ExecuteCommandAsync();
        return version;
    }
}
