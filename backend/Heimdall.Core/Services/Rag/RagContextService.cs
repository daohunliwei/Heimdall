using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>
/// RAG 上下文构建服务 — V2 双向量检索为主，旧 embedding_documents 为只读回退。
/// </summary>
public sealed class RagContextService
{
    private readonly IDualVectorSearchService _dualSearch;
    private readonly RepositoryEmbeddingService _legacyEmbedding;
    private readonly IWikiVersionRepository _wikiVersionRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IRepositoryVersionRepository _repoVersionRepo;
    private readonly ProviderRegistry _providerRegistry;
    private readonly ILogger<RagContextService> _logger;

    public RagContextService(
        IDualVectorSearchService dualSearch,
        RepositoryEmbeddingService legacyEmbedding,
        IWikiVersionRepository wikiVersionRepo,
        IWikiSpaceRepository spaceRepo,
        IRepositoryVersionRepository repoVersionRepo,
        ProviderRegistry providerRegistry,
        ILogger<RagContextService> logger)
    {
        _dualSearch = dualSearch;
        _legacyEmbedding = legacyEmbedding;
        _wikiVersionRepo = wikiVersionRepo;
        _spaceRepo = spaceRepo;
        _repoVersionRepo = repoVersionRepo;
        _providerRegistry = providerRegistry;
        _logger = logger;
    }

    public async Task<string> BuildRagContextAsync(string query, Guid repositoryId, int topK = 20, CancellationToken ct = default)
    {
        try
        {
            var embedder = _providerRegistry.ResolveEmbeddingProvider();
            var queryVector = await embedder.EmbedAsync(query, ct);

            // 尝试 V2 双向量检索
            var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "zh", "default");
            if (space?.PublishedWikiVersionId is not null)
            {
                var wikiVersionId = space.PublishedWikiVersionId.Value;
                var repoVersion = await _repoVersionRepo.GetLatestByRepoBranchAsync(repositoryId, "main");

                if (repoVersion is not null)
                {
                    var combined = await _dualSearch.SearchCombinedAsync(
                        queryVector, repoVersion.Id, wikiVersionId, topK, ct);

                    if (combined.CodeResults.Count > 0 || combined.WikiResults.Count > 0)
                    {
                        var parts = new List<string>();

                        foreach (var (chunk, sim) in combined.CodeResults.Take(topK / 2))
                        {
                            parts.Add($"// {chunk.FilePath}:{chunk.StartLine}-{chunk.EndLine} (相似度 {sim:F2})\n{chunk.ContentRaw}");
                        }

                        foreach (var (chunk, sim) in combined.WikiResults.Take(topK / 2))
                        {
                            parts.Add($"// Wiki: {chunk.ContentRaw} (相似度 {sim:F2})");
                        }

                        return string.Join("\n\n---\n\n", parts);
                    }
                }
            }

            // 回退到旧 embedding_documents 表（只读）
            _logger.LogDebug("双向量检索无结果，回退到旧 embedding_documents 表 RepoId={RepoId}", repositoryId);
            var legacyResults = await _legacyEmbedding.SearchAsync(repositoryId, query, topK, ct);
            if (legacyResults.Count == 0) return string.Empty;

            var legacyParts = legacyResults.Select(r =>
                $"// {r.FilePath} (chunk {r.ChunkIndex})\n{r.TextContent}");
            return string.Join("\n\n---\n\n", legacyParts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG 上下文构建失败，回退到旧表 RepoId={RepoId}", repositoryId);

            // 最终回退
            try
            {
                var legacyResults = await _legacyEmbedding.SearchAsync(repositoryId, query, topK, ct);
                if (legacyResults.Count == 0) return string.Empty;
                var legacyParts = legacyResults.Select(r =>
                    $"// {r.FilePath} (chunk {r.ChunkIndex})\n{r.TextContent}");
                return string.Join("\n\n---\n\n", legacyParts);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
