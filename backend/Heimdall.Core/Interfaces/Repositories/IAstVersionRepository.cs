using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IAstVersionRepository
{
    Task<AstVersion?> GetByIdAsync(Guid id);
    Task<AstVersion?> GetByRepoVersionAndConfigAsync(Guid repositoryVersionId, string configFingerprint);
    Task<List<AstVersion>> GetByRepositoryVersionIdAsync(Guid repositoryVersionId);
    Task<AstVersion> InsertAsync(AstVersion version);
    Task UpdateAsync(AstVersion version);
    Task SaveChangesAsync();
}
