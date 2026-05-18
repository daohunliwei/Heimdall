using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiEmbeddingRepository : IWikiEmbeddingRepository
{
    private readonly AppDbContext _context;
    public WikiEmbeddingRepository(AppDbContext context) => _context = context;

    public async Task<List<WikiEmbeddingChunk>> GetByVersionIdAsync(Guid wikiVersionId)
    {
        return await _context.WikiEmbeddingChunks
            .Where(c => c.WikiVersionId == wikiVersionId && c.EmbeddingVector != null)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<WikiEmbeddingChunk> AddAsync(WikiEmbeddingChunk chunk)
    {
        _context.WikiEmbeddingChunks.Add(chunk);
        await _context.SaveChangesAsync();
        return chunk;
    }

    public async Task AddRangeAsync(IEnumerable<WikiEmbeddingChunk> chunks)
    {
        _context.WikiEmbeddingChunks.AddRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteByVersionIdAsync(Guid wikiVersionId)
    {
        return await _context.WikiEmbeddingChunks
            .Where(c => c.WikiVersionId == wikiVersionId)
            .ExecuteDeleteAsync();
    }
}
