using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="WikiPage"/> entities.</summary>
public interface IWikiPageRepository
{
    Task<List<WikiPage>> GetByWikiIdAsync(Guid wikiId);

    /// <summary>
    /// 按 WikiVersion 标识读取页面。
    /// 该方法是版本化页面读取的主入口，避免再通过旧 Wiki 主表间接筛选版本数据。
    /// </summary>
    Task<List<WikiPage>> GetByWikiVersionIdAsync(Guid wikiVersionId);

    Task<WikiPage> AddAsync(WikiPage page);
    Task<List<WikiPage>> AddRangeAsync(IEnumerable<WikiPage> pages);
    Task<WikiPage> UpdateAsync(WikiPage page);
    Task<bool> DeleteByWikiIdAsync(Guid wikiId);
}
