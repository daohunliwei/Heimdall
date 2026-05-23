using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class RepositoryVersionRepository : IRepositoryVersionRepository
{
    private readonly ISqlSugarClient _db;

    public RepositoryVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<RepositoryVersion?> GetByIdAsync(Guid id)
    {
        return await _db.Queryable<RepositoryVersion>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<RepositoryVersion?> GetByRepoBranchCommitAsync(Guid repositoryId, string branchName, string commitSha)
    {
        return await _db.Queryable<RepositoryVersion>()
            .FirstAsync(v => v.RepositoryId == repositoryId
                && v.BranchName == branchName
                && v.CommitSha == commitSha);
    }

    public async Task<List<RepositoryVersion>> GetByRepositoryIdAsync(Guid repositoryId)
    {
        return await _db.Queryable<RepositoryVersion>()
            .Where(v => v.RepositoryId == repositoryId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<RepositoryVersion?> GetLatestByRepoBranchAsync(Guid repositoryId, string branchName)
    {
        return await _db.Queryable<RepositoryVersion>()
            .Where(v => v.RepositoryId == repositoryId && v.BranchName == branchName && v.IsLatestOnBranch)
            .OrderByDescending(v => v.CreatedAt)
            .FirstAsync();
    }

    public async Task<RepositoryVersion> AddAsync(RepositoryVersion version)
    {
        await _db.Insertable(version).ExecuteCommandAsync();
        return version;
    }

    public async Task UpdateAsync(RepositoryVersion version)
    {
        await _db.Updateable(version).ExecuteCommandAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<RepositoryVersion> versions)
    {
        await _db.Updateable(versions.ToList()).ExecuteCommandAsync();
    }

    public async Task SaveChangesAsync()
    {
        // SqlSugar 直接执行，无需显式保存
        await Task.CompletedTask;
    }
}
