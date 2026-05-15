using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>双向量检索服务实现 — 代码向量查询 + Wiki 向量查询 + 联合重排</summary>
public class DualVectorSearchService : IDualVectorSearchService
{
    private readonly ICodeEmbeddingRepository _codeRepo;
    private readonly IWikiEmbeddingRepository _wikiRepo;
    private readonly ILogger<DualVectorSearchService> _logger;

    public DualVectorSearchService(
        ICodeEmbeddingRepository codeRepo,
        IWikiEmbeddingRepository wikiRepo,
        ILogger<DualVectorSearchService> logger)
    {
        _codeRepo = codeRepo;
        _wikiRepo = wikiRepo;
        _logger = logger;
    }

    public async Task<List<(CodeEmbeddingChunk Chunk, float Similarity)>> SearchCodeAsync(
        float[] queryEmbedding, Guid repositoryVersionId, int topK = 10, CancellationToken cancellationToken = default)
    {
        var chunks = await _codeRepo.GetByVersionIdAsync(repositoryVersionId);
        if (chunks.Count == 0) return [];

        var results = new List<(CodeEmbeddingChunk Chunk, float Similarity)>();
        foreach (var chunk in chunks)
        {
            if (chunk.EmbeddingVector is null) continue;
            var chunkFloats = TextUtilityService.ConvertBytesToFloats(chunk.EmbeddingVector);
            var similarity = TextUtilityService.CosineSimilarity(queryEmbedding, chunkFloats);
            results.Add((Chunk: chunk, Similarity: similarity));
        }

        return results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
    }

    public async Task<List<(WikiEmbeddingChunk Chunk, float Similarity)>> SearchWikiAsync(
        float[] queryEmbedding, Guid wikiVersionId, int topK = 10, CancellationToken cancellationToken = default)
    {
        var chunks = await _wikiRepo.GetByVersionIdAsync(wikiVersionId);
        if (chunks.Count == 0) return [];

        var results = new List<(WikiEmbeddingChunk Chunk, float Similarity)>();
        foreach (var chunk in chunks)
        {
            if (chunk.EmbeddingVector is null) continue;
            var chunkFloats = TextUtilityService.ConvertBytesToFloats(chunk.EmbeddingVector);
            var similarity = TextUtilityService.CosineSimilarity(queryEmbedding, chunkFloats);
            results.Add((Chunk: chunk, Similarity: similarity));
        }

        return results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
    }

    public async Task<CombinedSearchResult> SearchCombinedAsync(
        float[] queryEmbedding, Guid repositoryVersionId, Guid? wikiVersionId = null, int topK = 10, CancellationToken cancellationToken = default)
    {
        // 注意：代码向量仓储与 Wiki 向量仓储默认共享同一个 Scoped DbContext。
        // 若在同一请求中并行触发两个 EF Core 查询，会触发
        // “A second operation was started on this context instance...” 异常。
        // 因此这里改为串行执行，优先保证 Ask/RAG 场景在 API 请求链路内稳定可用。
        var codeResults = await SearchCodeAsync(queryEmbedding, repositoryVersionId, topK, cancellationToken);
        var wikiResults = wikiVersionId.HasValue
            ? await SearchWikiAsync(queryEmbedding, wikiVersionId.Value, topK, cancellationToken)
            : [];

        return new CombinedSearchResult
        {
            CodeResults = codeResults,
            WikiResults = wikiResults,
            RerankSummary = $"代码块命中 {codeResults.Count} 条，Wiki 段落命中 {wikiResults.Count} 条"
        };
    }

    public async Task<int> DeleteCodeVectorsAsync(Guid repositoryVersionId, CancellationToken cancellationToken = default)
    {
        return await _codeRepo.DeleteByVersionIdAsync(repositoryVersionId);
    }

    public async Task<int> DeleteWikiVectorsAsync(Guid wikiVersionId, CancellationToken cancellationToken = default)
    {
        return await _wikiRepo.DeleteByVersionIdAsync(wikiVersionId);
    }
}
