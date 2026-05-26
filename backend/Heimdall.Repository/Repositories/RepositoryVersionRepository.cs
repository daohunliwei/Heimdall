using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class RepositoryVersionRepository : BaseRepository<RepositoryVersion>, IRepositoryVersionRepository
{
    public RepositoryVersionRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<RepositoryVersion?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<RepositoryVersion>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<RepositoryVersion?> GetByRepoBranchCommitAsync(Guid repositoryId, string branchName, string commitSha)
    {
        return await Context.Queryable<RepositoryVersion>()
            .FirstAsync(v => v.RepositoryId == repositoryId
                && v.BranchName == branchName
                && v.CommitSha == commitSha);
    }

    public async Task<List<RepositoryVersion>> GetByRepositoryIdAsync(Guid repositoryId)
    {
        return await Context.Queryable<RepositoryVersion>()
            .Where(v => v.RepositoryId == repositoryId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<RepositoryVersion?> GetLatestByRepoBranchAsync(Guid repositoryId, string branchName)
    {
        return await Context.Queryable<RepositoryVersion>()
            .Where(v => v.RepositoryId == repositoryId && v.BranchName == branchName && v.IsLatestOnBranch)
            .OrderByDescending(v => v.CreatedAt)
            .FirstAsync();
    }

    public async Task<RepositoryVersion> AddAsync(RepositoryVersion version)
    {
        await Context.Insertable(version).ExecuteCommandAsync();
        return version;
    }

    public async Task UpdateAsync(RepositoryVersion version)
    {
        await Context.Updateable(version).ExecuteCommandAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<RepositoryVersion> versions)
    {
        await Context.Updateable(versions.ToList()).PageSize(1000).ExecuteCommandAsync();
    }

    public async Task SaveChangesAsync()
    {
        // SqlSugar 直接执行，无需显式保存
        await Task.CompletedTask;
    }
}
