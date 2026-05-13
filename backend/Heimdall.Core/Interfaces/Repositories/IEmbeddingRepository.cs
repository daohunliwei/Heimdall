using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="EmbeddingDocument"/> entities with vector search.</summary>
public interface IEmbeddingRepository
{
    Task<EmbeddingDocument> AddAsync(EmbeddingDocument document);
    Task<List<EmbeddingDocument>> AddRangeAsync(IEnumerable<EmbeddingDocument> documents);

    /// <summary>Cosine-similarity search returning the top-K nearest documents and their similarity scores.</summary>
    Task<List<(EmbeddingDocument Document, float Similarity)>> SearchSimilarAsync(float[] queryEmbedding, int topK, Guid? repositoryId = null);

    /// <summary>Delete all embedding documents belonging to a repository.</summary>
    Task<int> DeleteByRepoAsync(Guid repositoryId);
}
