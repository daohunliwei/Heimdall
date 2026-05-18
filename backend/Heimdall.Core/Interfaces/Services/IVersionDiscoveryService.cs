using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>仓库版本发现服务接口</summary>
public interface IVersionDiscoveryService
{
    /// <summary>发现仓库快照版本：查询远端 HEAD → 比较本地 → 创建或复用 repository_version</summary>
    Task<RepositoryVersion> DiscoverRepositoryVersionAsync(Guid repositoryId, string branch, CancellationToken cancellationToken = default);

    /// <summary>获取指定仓库、分支的最新版本</summary>
    Task<RepositoryVersion?> GetLatestVersionAsync(Guid repositoryId, string branch, CancellationToken cancellationToken = default);

    /// <summary>获取仓库的所有已知版本列表</summary>
    Task<List<RepositoryVersion>> GetVersionsAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}
