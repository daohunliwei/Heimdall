using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>双向量检索服务接口 — 代码向量与 Wiki 向量分治检索</summary>
public interface IDualVectorSearchService
{
    /// <summary>按仓库版本检索代码向量</summary>
    Task<List<(CodeEmbeddingChunk Chunk, float Similarity)>> SearchCodeAsync(
        float[] queryEmbedding, Guid repositoryVersionId, int topK = 10, CancellationToken cancellationToken = default);

    /// <summary>按 Wiki 版本检索 Wiki 向量</summary>
    Task<List<(WikiEmbeddingChunk Chunk, float Similarity)>> SearchWikiAsync(
        float[] queryEmbedding, Guid wikiVersionId, int topK = 10, CancellationToken cancellationToken = default);

    /// <summary>双向量域联合召回 + 结果重排</summary>
    Task<CombinedSearchResult> SearchCombinedAsync(
        float[] queryEmbedding, Guid repositoryVersionId, Guid? wikiVersionId = null, int topK = 10, CancellationToken cancellationToken = default);

    /// <summary>删除指定仓库版本的所有代码向量</summary>
    Task<int> DeleteCodeVectorsAsync(Guid repositoryVersionId, CancellationToken cancellationToken = default);

    /// <summary>删除指定 Wiki 版本的所有 Wiki 向量</summary>
    Task<int> DeleteWikiVectorsAsync(Guid wikiVersionId, CancellationToken cancellationToken = default);
}

public class CombinedSearchResult
{
    public List<(CodeEmbeddingChunk Chunk, float Similarity)> CodeResults { get; set; } = [];
    public List<(WikiEmbeddingChunk Chunk, float Similarity)> WikiResults { get; set; } = [];
    /// <summary>重排后的合并结果描述</summary>
    public string? RerankSummary { get; set; }
}
