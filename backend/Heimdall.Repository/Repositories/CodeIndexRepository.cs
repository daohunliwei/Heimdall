using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public sealed class CodeIndexRepository : ICodeIndexRepository
{
    private readonly AppDbContext _db;

    public CodeIndexRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CodeIndexEntry>> GetByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        return await _db.CodeIndexEntries
            .AsNoTracking()
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .Include(e => e.Chunks)
            .ToListAsync(ct);
    }

    public async Task AddEntriesAsync(List<CodeIndexEntry> entries, CancellationToken ct = default)
    {
        await _db.CodeIndexEntries.AddRangeAsync(entries, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteByVersionIdAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        var existing = await _db.CodeIndexEntries
            .Where(e => e.RepositoryVersionId == repositoryVersionId)
            .ToListAsync(ct);
        _db.CodeIndexEntries.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
    }
}
