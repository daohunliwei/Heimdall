using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class WikiSpaceRepository : BaseRepository<WikiSpace>, IWikiSpaceRepository
{
    public WikiSpaceRepository(ISqlSugarClient db) : base(db) { }

    /// <summary>
    /// 按主键读取 Wiki 空间。
    /// </summary>
    public async Task<WikiSpace?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<WikiSpace>().FirstAsync(space => space.Id == id);
    }

    public async Task<WikiSpace?> GetByRepoLangViewAsync(Guid repositoryId, string language, string viewType)
    {
        return await Context.Queryable<WikiSpace>()
            .FirstAsync(s => s.RepositoryId == repositoryId
                && s.Language == language && s.ViewType == viewType);
    }

    public async Task<WikiSpace> AddAsync(WikiSpace space)
    {
        await Context.Insertable(space).ExecuteCommandAsync();
        return space;
    }

    public async Task<WikiSpace> UpdateAsync(WikiSpace space)
    {
        await Context.Updateable(space).ExecuteCommandAsync();
        return space;
    }
}
