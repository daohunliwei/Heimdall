using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class AstVersionRepository : BaseRepository<AstVersion>, IAstVersionRepository
{
    public AstVersionRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<AstVersion?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<AstVersion>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<AstVersion?> GetByRepoVersionAndConfigAsync(Guid repositoryVersionId, string configFingerprint)
    {
        return await Context.Queryable<AstVersion>()
            .Where(v => v.RepositoryVersionId == repositoryVersionId
                && v.ConfigFingerprint == configFingerprint
                && v.Status == "success")
            .OrderByDescending(v => v.CreatedAt)
            .FirstAsync();
    }

    public async Task<List<AstVersion>> GetByRepositoryVersionIdAsync(Guid repositoryVersionId)
    {
        return await Context.Queryable<AstVersion>()
            .Where(v => v.RepositoryVersionId == repositoryVersionId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<AstVersion> InsertAsync(AstVersion version)
    {
        await Context.Insertable(version).ExecuteCommandAsync();
        return version;
    }

    public async Task UpdateAsync(AstVersion version)
    {
        await Context.Updateable(version).ExecuteCommandAsync();
    }

    public async Task SaveChangesAsync()
    {
        await Task.CompletedTask;
    }
}
