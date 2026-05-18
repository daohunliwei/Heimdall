using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>仓库版本仓储接口</summary>
public interface IRepositoryVersionRepository
{
    Task<RepositoryVersion?> GetByIdAsync(Guid id);
    Task<RepositoryVersion?> GetByRepoBranchCommitAsync(Guid repositoryId, string branchName, string commitSha);
    Task<List<RepositoryVersion>> GetByRepositoryIdAsync(Guid repositoryId);
    Task<RepositoryVersion?> GetLatestByRepoBranchAsync(Guid repositoryId, string branchName);
    Task<RepositoryVersion> AddAsync(RepositoryVersion version);
    Task UpdateAsync(RepositoryVersion version);
    Task UpdateRangeAsync(IEnumerable<RepositoryVersion> versions);
    Task SaveChangesAsync();
}
