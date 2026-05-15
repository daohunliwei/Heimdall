using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>
/// Wiki 页面数据访问接口。
/// V4：已移除旧 WikiId 相关方法，页面读写统一通过 WikiVersionId。
/// </summary>
public interface IWikiPageRepository
{
    /// <summary>按 WikiVersionId 读取页面集合（版本化读取主入口）</summary>
    Task<List<WikiPage>> GetByWikiVersionIdAsync(Guid wikiVersionId);

    /// <summary>新增页面</summary>
    Task<WikiPage> AddAsync(WikiPage page);

    /// <summary>批量新增页面</summary>
    Task<List<WikiPage>> AddRangeAsync(IEnumerable<WikiPage> pages);

    /// <summary>更新页面</summary>
    Task<WikiPage> UpdateAsync(WikiPage page);
}
