using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class RepositoryVersionRepository : IRepositoryVersionRepository
{
    private readonly AppDbContext _context;

    public RepositoryVersionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RepositoryVersion?> GetByIdAsync(Guid id)
    {
        return await _context.RepositoryVersions.FindAsync(id);
    }

    public async Task<RepositoryVersion?> GetByRepoBranchCommitAsync(Guid repositoryId, string branchName, string commitSha)
    {
        return await _context.RepositoryVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.RepositoryId == repositoryId
                && v.BranchName == branchName
                && v.CommitSha == commitSha);
    }

    public async Task<List<RepositoryVersion>> GetByRepositoryIdAsync(Guid repositoryId)
    {
        return await _context.RepositoryVersions
            .AsNoTracking()
            .Where(v => v.RepositoryId == repositoryId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<RepositoryVersion?> GetLatestByRepoBranchAsync(Guid repositoryId, string branchName)
    {
        return await _context.RepositoryVersions
            .AsNoTracking()
            .Where(v => v.RepositoryId == repositoryId && v.BranchName == branchName && v.IsLatestOnBranch)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<RepositoryVersion> AddAsync(RepositoryVersion version)
    {
        _context.RepositoryVersions.Add(version);
        await _context.SaveChangesAsync();
        return version;
    }

    public async Task UpdateAsync(RepositoryVersion version)
    {
        _context.RepositoryVersions.Update(version);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<RepositoryVersion> versions)
    {
        _context.RepositoryVersions.UpdateRange(versions);
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
