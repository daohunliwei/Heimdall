using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface ICodeIndexRepository
{
    Task<List<CodeIndexEntry>> GetByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default);
    Task AddEntriesAsync(List<CodeIndexEntry> entries, CancellationToken ct = default);
    Task DeleteByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default);
}
