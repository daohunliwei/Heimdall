using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="Repository"/> entities (repository configuration).</summary>
public interface IRepositoryConfigRepository
{
    Task<Repository?> GetByIdAsync(Guid id);
    Task<Repository?> GetByOwnerRepoTypeAsync(string owner, string repoName, string repoType);
    Task<Repository?> GetByOwnerRepoAnyTypeAsync(string owner, string repoName);
    Task<Repository?> GetByProviderKeyAsync(string providerType, string providerRepositoryKey);
    Task<List<Repository>> GetAllAsync();
    Task<Repository> AddAsync(Repository repository);
    Task<Repository> UpdateAsync(Repository repository);
    Task<bool> DeleteAsync(Guid id);
}
