using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="WikiPage"/> entities.</summary>
public interface IWikiPageRepository
{
    Task<List<WikiPage>> GetByWikiIdAsync(Guid wikiId);
    Task<WikiPage> AddAsync(WikiPage page);
    Task<List<WikiPage>> AddRangeAsync(IEnumerable<WikiPage> pages);
    Task<WikiPage> UpdateAsync(WikiPage page);
    Task<bool> DeleteByWikiIdAsync(Guid wikiId);
}
