using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public sealed class CodeIndexRepository : BaseRepository<CodeIndexEntry>, ICodeIndexRepository
{
    public CodeIndexRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<CodeIndexEntry>> GetByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        return await Context.Queryable<CodeIndexEntry>()
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .ToListAsync(ct);
    }

    public async Task AddEntriesAsync(List<CodeIndexEntry> entries, CancellationToken ct = default)
    {
        await Context.Insertable(entries).ExecuteCommandAsync(ct);
    }

    public async Task DeleteByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        await Context.Deleteable<CodeIndexEntry>()
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .ExecuteCommandAsync(ct);
    }
}
