namespace Heimdall.Core.Interfaces.Services;

/// <summary>Repository embedding: index and search repository documents via vector embeddings.</summary>
public interface IRepositoryEmbeddingService
{
    /// <summary>Embed all documents in a repository (or a specific path within it).</summary>
    Task EmbedRepoAsync(Guid repoId, string path);
    /// <summary>Search embedded documents by semantic similarity.</summary>
    Task<List<EmbeddingSearchResult>> SearchAsync(Guid repoId, string query, int topK);
}

/// <summary>A single embedding search result.</summary>
public class EmbeddingSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public float Score { get; init; }
}
