using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Wiki 内容向量块仓储接口</summary>
public interface IWikiEmbeddingRepository
{
    Task<List<WikiEmbeddingChunk>> GetByVersionIdAsync(Guid wikiVersionId);
    Task<WikiEmbeddingChunk> AddAsync(WikiEmbeddingChunk chunk);
    Task AddRangeAsync(IEnumerable<WikiEmbeddingChunk> chunks);
    Task<int> DeleteByVersionIdAsync(Guid wikiVersionId);
}
