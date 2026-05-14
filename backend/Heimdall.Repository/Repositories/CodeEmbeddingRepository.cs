using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class CodeEmbeddingRepository : ICodeEmbeddingRepository
{
    private readonly AppDbContext _context;
    public CodeEmbeddingRepository(AppDbContext context) => _context = context;

    public async Task<List<CodeEmbeddingChunk>> GetByVersionIdAsync(Guid repositoryVersionId)
    {
        return await _context.CodeEmbeddingChunks
            .Where(c => c.RepositoryVersionId == repositoryVersionId && c.EmbeddingVector != null)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<CodeEmbeddingChunk> AddAsync(CodeEmbeddingChunk chunk)
    {
        _context.CodeEmbeddingChunks.Add(chunk);
        await _context.SaveChangesAsync();
        return chunk;
    }

    public async Task AddRangeAsync(IEnumerable<CodeEmbeddingChunk> chunks)
    {
        _context.CodeEmbeddingChunks.AddRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteByVersionIdAsync(Guid repositoryVersionId)
    {
        return await _context.CodeEmbeddingChunks
            .Where(c => c.RepositoryVersionId == repositoryVersionId)
            .ExecuteDeleteAsync();
    }
}
