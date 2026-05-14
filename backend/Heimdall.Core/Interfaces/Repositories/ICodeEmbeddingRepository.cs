using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>代码向量块仓储接口</summary>
public interface ICodeEmbeddingRepository
{
    Task<List<CodeEmbeddingChunk>> GetByVersionIdAsync(Guid repositoryVersionId);
    Task<CodeEmbeddingChunk> AddAsync(CodeEmbeddingChunk chunk);
    Task AddRangeAsync(IEnumerable<CodeEmbeddingChunk> chunks);
    Task<int> DeleteByVersionIdAsync(Guid repositoryVersionId);
}
