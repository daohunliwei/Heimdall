using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiVersionRepository : IWikiVersionRepository
{
    private readonly ISqlSugarClient _db;
    public WikiVersionRepository(ISqlSugarClient db) => _db = db;

    public async Task<WikiVersion?> GetByIdAsync(Guid id)
    {
        return await _db.Queryable<WikiVersion>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<List<WikiVersion>> GetBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await _db.Queryable<WikiVersion>()
            .Where(v => v.WikiSpaceId == wikiSpaceId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> CountBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await _db.Queryable<WikiVersion>().CountAsync(v => v.WikiSpaceId == wikiSpaceId);
    }

    public async Task<WikiVersion> AddAsync(WikiVersion version)
    {
        await _db.Insertable(version).ExecuteCommandAsync();
        return version;
    }

    public async Task<WikiVersion> UpdateAsync(WikiVersion version)
    {
        await _db.Updateable(version).ExecuteCommandAsync();
        return version;
    }
}
