using Heimdall.Core.Entities;
using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>
/// Wiki 任务执行仓储接口。
/// 该接口用于承接需要跨多张表事务提交的持久化逻辑，
/// 以保证 Core 层不直接依赖具体数据访问实现。
/// </summary>
public interface IWikiTaskExecutionRepository
{
    /// <summary>
    /// 在单一事务内持久化 Wiki 主数据、版本数据、页面数据、页面关系与渲染快照。
    /// </summary>
    Task<(Guid WikiId, Guid RepositoryVersionId, Guid WikiVersionId, List<WikiPage> Pages)> PersistWikiProjectionAsync(
        TaskRecord task,
        WikiStructureDto structure,
        string structureJson,
        string language,
        string branch,
        string generationProfile,
        CancellationToken cancellationToken = default);
}
