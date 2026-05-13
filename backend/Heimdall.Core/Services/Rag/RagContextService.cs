using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>
/// RAG 上下文构建服务。
/// </summary>
public sealed class RagContextService
{
    private readonly RepositoryEmbeddingService _embeddingService;
    private readonly ILogger<RagContextService> _logger;

    public RagContextService(RepositoryEmbeddingService embeddingService, ILogger<RagContextService> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<string> BuildRagContextAsync(string query, Guid repositoryId, int topK = 20, CancellationToken ct = default)
    {
        var results = await _embeddingService.SearchAsync(repositoryId, query, topK, ct);

        if (results.Count == 0)
            return string.Empty;

        var contextParts = results.Select(r =>
            $"// {r.FilePath} (chunk {r.ChunkIndex})\n{r.TextContent}");

        return string.Join("\n\n---\n\n", contextParts);
    }
}
