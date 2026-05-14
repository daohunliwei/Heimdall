using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Infrastructure.Utilities;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class EmbeddingRepository : IEmbeddingRepository
{
    private readonly AppDbContext _context;

    public EmbeddingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EmbeddingDocument> AddAsync(EmbeddingDocument document)
    {
        document.CreatedAt = DateTime.UtcNow;
        _context.EmbeddingDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<List<EmbeddingDocument>> AddRangeAsync(IEnumerable<EmbeddingDocument> documents)
    {
        var docList = documents.ToList();
        foreach (var doc in docList)
        {
            doc.CreatedAt = DateTime.UtcNow;
        }

        _context.EmbeddingDocuments.AddRange(docList);
        await _context.SaveChangesAsync();
        return docList;
    }

    public async Task<List<(EmbeddingDocument Document, float Similarity)>> SearchSimilarAsync(
        float[] queryEmbedding, int topK, Guid? repositoryId = null)
    {
        var query = _context.EmbeddingDocuments.AsQueryable();
        if (repositoryId.HasValue)
            query = query.Where(e => e.RepositoryId == repositoryId.Value);

        var docs = await query.Where(e => e.Embedding != null).ToListAsync();

        var results = docs
            .Where(d => d.Embedding != null)
            .Select(d =>
            {
                var vector = TextUtilityService.ConvertBytesToFloats(d.Embedding!);
                var similarity = TextUtilityService.CosineSimilarity(queryEmbedding, vector);
                return (Document: d, Similarity: similarity);
            })
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();

        return results;
    }

    public async Task<int> DeleteByRepoAsync(Guid repositoryId)
    {
        return await _context.EmbeddingDocuments
            .Where(d => d.RepositoryId == repositoryId)
            .ExecuteDeleteAsync();
    }
}
