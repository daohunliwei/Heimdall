using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>
/// 仓库嵌入服务，使用 pgvector 作为向量存储。
/// </summary>
public sealed class RepositoryEmbeddingService
{
    private readonly IEmbeddingRepository _embeddingRepo;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TextUtilityService _textUtility;
    private readonly ILogger<RepositoryEmbeddingService> _logger;

    public RepositoryEmbeddingService(
        IEmbeddingRepository embeddingRepo,
        ProviderRegistry providerRegistry,
        TextUtilityService textUtility,
        ILogger<RepositoryEmbeddingService> logger)
    {
        _embeddingRepo = embeddingRepo;
        _providerRegistry = providerRegistry;
        _textUtility = textUtility;
        _logger = logger;
    }

    public async Task EmbedRepoAsync(Guid repositoryId, string repositoryPath, List<EmbeddedDocument> documents, CancellationToken ct)
    {
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var embeddingDocuments = new List<EmbeddingDocument>();

        foreach (var doc in documents)
        {
            ct.ThrowIfCancellationRequested();

            // 文本切分
            var chunks = _textUtility.SplitByWords(doc.Text, 350, 100);
            for (var i = 0; i < chunks.Count; i++)
            {
                var vector = await embedder.EmbedAsync(chunks[i], ct);
                embeddingDocuments.Add(new EmbeddingDocument
                {
                    RepositoryId = repositoryId,
                    FilePath = doc.FilePath,
                    ChunkIndex = i,
                    TextContent = chunks[i],
                    Embedding = TextUtilityService.ConvertFloatsToBytes(vector),
                    TokenCount = _textUtility.EstimateTokenCount(chunks[i]),
                    IsCode = doc.IsCode
                });
            }
        }

        await _embeddingRepo.DeleteByRepoAsync(repositoryId);
        await _embeddingRepo.AddRangeAsync(embeddingDocuments);

        _logger.LogInformation(
            "嵌入完成 RepositoryId={RepoId} Documents={DocCount} Chunks={ChunkCount}",
            repositoryId, documents.Count, embeddingDocuments.Count);
    }

    public async Task<List<EmbeddingDocument>> SearchAsync(Guid repositoryId, string query, int topK = 20, CancellationToken ct = default)
    {
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var queryVector = await embedder.EmbedAsync(query, ct);

        var results = await _embeddingRepo.SearchSimilarAsync(queryVector, topK, repositoryId);
        return results.Select(r => r.Document).ToList();
    }
}
