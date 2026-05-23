using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiSpaceRepository : IWikiSpaceRepository
{
    private readonly ISqlSugarClient _db;
    public WikiSpaceRepository(ISqlSugarClient db) => _db = db;

    /// <summary>
    /// 按主键读取 Wiki 空间。
    /// </summary>
    public async Task<WikiSpace?> GetByIdAsync(Guid id)
    {
        return await _db.Queryable<WikiSpace>().FirstAsync(space => space.Id == id);
    }

    public async Task<WikiSpace?> GetByRepoLangViewAsync(Guid repositoryId, string language, string viewType)
    {
        return await _db.Queryable<WikiSpace>()
            .FirstAsync(s => s.RepositoryId == repositoryId
                && s.Language == language && s.ViewType == viewType);
    }

    public async Task<WikiSpace> AddAsync(WikiSpace space)
    {
        await _db.Insertable(space).ExecuteCommandAsync();
        return space;
    }

    public async Task<WikiSpace> UpdateAsync(WikiSpace space)
    {
        await _db.Updateable(space).ExecuteCommandAsync();
        return space;
    }
}
