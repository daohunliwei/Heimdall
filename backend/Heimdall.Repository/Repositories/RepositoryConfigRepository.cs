using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Repository.Repositories;

public class RepositoryConfigRepository : BaseRepository<RepositoryEntity>, IRepositoryConfigRepository
{
    public RepositoryConfigRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<RepositoryEntity?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<RepositoryEntity>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoTypeAsync(string owner, string repoName, string repoType)
    {
        return await Context.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.Owner == owner
                && r.RepoName == repoName
                && r.RepoType == repoType);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoAnyTypeAsync(string owner, string repoName)
    {
        return await Context.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.Owner == owner && r.RepoName == repoName);
    }

    public async Task<RepositoryEntity?> GetByProviderKeyAsync(string providerType, string providerRepositoryKey)
    {
        return await Context.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.ProviderType == providerType
                && r.ProviderRepositoryKey == providerRepositoryKey);
    }

    public async Task<List<RepositoryEntity>> GetAllAsync()
    {
        return await Context.Queryable<RepositoryEntity>()
            .OrderBy(r => new { r.Owner, r.RepoName })
            .ToListAsync();
    }

    public async Task<RepositoryEntity> AddAsync(RepositoryEntity repository)
    {
        repository.CreatedAt = DateTime.UtcNow;
        repository.UpdatedAt = DateTime.UtcNow;
        await Context.Insertable(repository).ExecuteCommandAsync();
        return repository;
    }

    public async Task<RepositoryEntity> UpdateAsync(RepositoryEntity repository)
    {
        repository.UpdatedAt = DateTime.UtcNow;
        await Context.Updateable(repository).ExecuteCommandAsync();
        return repository;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Context.Deleteable<RepositoryEntity>()
            .Where(x => x.Id == id).ExecuteCommandAsync() > 0;
    }

    public async Task<List<RepositoryEntity>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return new List<RepositoryEntity>();
        return await Context.Queryable<RepositoryEntity>()
            .Where(r => idList.Contains(r.Id)).ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return await Context.Queryable<RepositoryEntity>().CountAsync();
    }
}
