using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IWikiSpaceRepository
{
    /// <summary>
    /// 按主键读取 Wiki 空间。
    /// 该方法主要用于版本解析阶段校验 WikiVersion 与仓库、语言的归属关系。
    /// </summary>
    Task<WikiSpace?> GetByIdAsync(Guid id);

    Task<WikiSpace?> GetByRepoLangViewAsync(Guid repositoryId, string language, string viewType);
    Task<List<WikiSpace>> GetByRepoIdsAsync(IEnumerable<Guid> repositoryIds);
    Task<WikiSpace> AddAsync(WikiSpace space);
    Task<WikiSpace> UpdateAsync(WikiSpace space);
}
