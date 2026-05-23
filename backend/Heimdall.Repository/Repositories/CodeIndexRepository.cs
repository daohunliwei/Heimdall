using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public sealed class CodeIndexRepository : ICodeIndexRepository
{
    private readonly ISqlSugarClient _db;

    public CodeIndexRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<List<CodeIndexEntry>> GetByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        return await _db.Queryable<CodeIndexEntry>()
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .ToListAsync(ct);
    }

    public async Task AddEntriesAsync(List<CodeIndexEntry> entries, CancellationToken ct = default)
    {
        await _db.Insertable(entries).ExecuteCommandAsync(ct);
    }

    public async Task DeleteByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        await _db.Deleteable<CodeIndexEntry>()
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .ExecuteCommandAsync(ct);
    }
}
