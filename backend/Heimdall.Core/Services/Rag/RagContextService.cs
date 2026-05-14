using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>
/// RAG 上下文构建服务 — 基于双向量检索（code_embedding_chunks + wiki_embedding_chunks）。
/// </summary>
public sealed class RagContextService
{
    private readonly IDualVectorSearchService _dualSearch;
    private readonly IWikiVersionRepository _wikiVersionRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IRepositoryVersionRepository _repoVersionRepo;
    private readonly ProviderRegistry _providerRegistry;
    private readonly ILogger<RagContextService> _logger;

    public RagContextService(
        IDualVectorSearchService dualSearch,
        IWikiVersionRepository wikiVersionRepo,
        IWikiSpaceRepository spaceRepo,
        IRepositoryVersionRepository repoVersionRepo,
        ProviderRegistry providerRegistry,
        ILogger<RagContextService> logger)
    {
        _dualSearch = dualSearch;
        _wikiVersionRepo = wikiVersionRepo;
        _spaceRepo = spaceRepo;
        _repoVersionRepo = repoVersionRepo;
        _providerRegistry = providerRegistry;
        _logger = logger;
    }

    public async Task<string> BuildRagContextAsync(string query, Guid repositoryId, int topK = 20, CancellationToken ct = default)
    {
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var queryVector = await embedder.EmbedAsync(query, ct);

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "zh", "default");
        Guid? wikiVersionId = space?.PublishedWikiVersionId;
        var repoVersion = await _repoVersionRepo.GetLatestByRepoBranchAsync(repositoryId, "main");

        if (repoVersion is null)
            return string.Empty;

        var combined = await _dualSearch.SearchCombinedAsync(
            queryVector, repoVersion.Id, wikiVersionId, topK, ct);

        if (combined.CodeResults.Count == 0 && combined.WikiResults.Count == 0)
            return string.Empty;

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
