using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>仓库主数据服务接口</summary>
public interface IRepositoryService
{
    /// <summary>根据仓库 URL 导入或复用仓库记录，返回 repositoryId</summary>
    Task<Repository> ImportAsync(string repoUrl, CancellationToken cancellationToken = default);

    /// <summary>获取所有仓库列表</summary>
    Task<List<Repository>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>根据 ID 获取仓库详情</summary>
    Task<Repository?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    /// <summary>更新仓库元数据</summary>
    Task<Repository?> UpdateAsync(Guid repositoryId, Action<Repository> patch, CancellationToken cancellationToken = default);

    /// <summary>删除仓库及其关联数据</summary>
    Task<bool> DeleteAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}
