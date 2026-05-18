using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiVersionRepository : IWikiVersionRepository
{
    private readonly AppDbContext _context;
    public WikiVersionRepository(AppDbContext context) => _context = context;

    public async Task<WikiVersion?> GetByIdAsync(Guid id)
    {
        return await _context.WikiVersions.FindAsync(id);
    }

    public async Task<List<WikiVersion>> GetBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await _context.WikiVersions
            .Where(v => v.WikiSpaceId == wikiSpaceId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> CountBySpaceIdAsync(Guid wikiSpaceId)
    {
        return await _context.WikiVersions.CountAsync(v => v.WikiSpaceId == wikiSpaceId);
    }

    public async Task<WikiVersion> AddAsync(WikiVersion version)
    {
        _context.WikiVersions.Add(version);
        await _context.SaveChangesAsync();
        return version;
    }

    public async Task<WikiVersion> UpdateAsync(WikiVersion version)
    {
        _context.WikiVersions.Update(version);
        await _context.SaveChangesAsync();
        return version;
    }
}
