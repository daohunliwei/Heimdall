using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>Wiki cache: store and retrieve generated wiki data keyed by repository.</summary>
public interface IWikiCacheService
{
    /// <summary>Get cached wiki data for a repository.</summary>
    Task<WikiCacheData?> GetAsync(Guid repoId);
    /// <summary>Save wiki and its pages to the cache.</summary>
    Task SaveAsync(Wiki wiki, List<WikiPage> pages);
    /// <summary>Invalidate cached wiki data for a repository.</summary>
    Task InvalidateAsync(Guid repoId);
}

/// <summary>Cached wiki data transfer object.</summary>
public class WikiCacheData
{
    public Wiki Wiki { get; init; } = null!;
    public List<WikiPage> Pages { get; init; } = new();
}
