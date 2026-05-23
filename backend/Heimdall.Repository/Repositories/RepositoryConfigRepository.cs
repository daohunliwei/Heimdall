using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Repository.Repositories;

public class RepositoryConfigRepository : IRepositoryConfigRepository
{
    private readonly ISqlSugarClient _db;

    public RepositoryConfigRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<RepositoryEntity?> GetByIdAsync(Guid id)
    {
        return await _db.Queryable<RepositoryEntity>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoTypeAsync(string owner, string repoName, string repoType)
    {
        return await _db.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.Owner == owner
                && r.RepoName == repoName
                && r.RepoType == repoType);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoAnyTypeAsync(string owner, string repoName)
    {
        return await _db.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.Owner == owner && r.RepoName == repoName);
    }

    public async Task<RepositoryEntity?> GetByProviderKeyAsync(string providerType, string providerRepositoryKey)
    {
        return await _db.Queryable<RepositoryEntity>()
            .FirstAsync(r => r.ProviderType == providerType
                && r.ProviderRepositoryKey == providerRepositoryKey);
    }

    public async Task<List<RepositoryEntity>> GetAllAsync()
    {
        return await _db.Queryable<RepositoryEntity>()
            .OrderBy(r => r.Owner)
            .OrderBy(r => r.RepoName)
            .ToListAsync();
    }

    public async Task<RepositoryEntity> AddAsync(RepositoryEntity repository)
    {
        repository.CreatedAt = DateTime.UtcNow;
        repository.UpdatedAt = DateTime.UtcNow;
        await _db.Insertable(repository).ExecuteCommandAsync();
        return repository;
    }

    public async Task<RepositoryEntity> UpdateAsync(RepositoryEntity repository)
    {
        repository.UpdatedAt = DateTime.UtcNow;
        await _db.Updateable(repository).ExecuteCommandAsync();
        return repository;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var repository = await _db.Queryable<RepositoryEntity>()
            .FirstAsync(x => x.Id == id);
        if (repository is null) return false;
        await _db.Deleteable(repository).ExecuteCommandAsync();
        return true;
    }
}
