namespace Heimdall.Core.Interfaces.Services;

/// <summary>RAG context builder: retrieve and assemble relevant documents for a query.</summary>
public interface IRagContextService
{
    /// <summary>Build a RAG context string from the top-K relevant documents in a repository.</summary>
    Task<string> BuildRagContextAsync(string query, Guid repoId, int topK);
}
