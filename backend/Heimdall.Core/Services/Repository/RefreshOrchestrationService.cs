using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

public class RefreshOrchestrationService : IRefreshOrchestrationService
{
    private readonly IVersionDiscoveryService _versionDiscovery;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly ILogger<RefreshOrchestrationService> _logger;

    public RefreshOrchestrationService(
        IVersionDiscoveryService versionDiscovery,
        IRepositoryConfigRepository repoRepo,
        ILogger<RefreshOrchestrationService> logger)
    {
        _versionDiscovery = versionDiscovery;
        _repoRepo = repoRepo;
        _logger = logger;
    }

    public async Task<RefreshResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var repo = await _repoRepo.GetByIdAsync(request.RepositoryId);
        if (repo is null)
            throw new InvalidOperationException($"仓库不存在：{request.RepositoryId}");

        // 如果是"刷新当前版本"策略，使用当前已知版本
        if (string.Equals(request.RefreshStrategy, "current", StringComparison.OrdinalIgnoreCase))
        {
            var currentVersion = await _versionDiscovery.GetLatestVersionAsync(request.RepositoryId, request.Branch, cancellationToken);
            var result = new RefreshResult
            {
                RepositoryId = request.RepositoryId,
                RepositoryVersionId = currentVersion?.Id,
                RefreshStrategy = "current",
                ChangeStatus = "unchanged",
                ResultType = request.ForceRefresh ? "queued" : "reused",
                Message = request.ForceRefresh ? "将基于当前版本强制重新生成" : "当前版本已存在，复用已有结果"
            };
            return result;
        }

        // "刷新最新版本"策略：查询远端 HEAD 并发现新版本
        try
        {
            var latestVersion = await _versionDiscovery.GetLatestVersionAsync(request.RepositoryId, request.Branch, cancellationToken);
            var discoveredVersion = await _versionDiscovery.DiscoverRepositoryVersionAsync(request.RepositoryId, request.Branch, cancellationToken);

            if (latestVersion is not null && latestVersion.CommitSha == discoveredVersion.CommitSha)
            {
                // 无新版本
                var result = new RefreshResult
                {
                    RepositoryId = request.RepositoryId,
                    RepositoryVersionId = latestVersion.Id,
                    RefreshStrategy = "latest",
                    ChangeStatus = "unchanged",
                    ResultType = request.ForceRefresh ? "queued" : "reused",
                    Message = request.ForceRefresh ? "无新版本，将基于当前版本强制重新生成" : "无新版本，复用已有结果"
                };
                return result;
            }

            // 发现了新版本
            return new RefreshResult
            {
                RepositoryId = request.RepositoryId,
                RepositoryVersionId = discoveredVersion.Id,
                RefreshStrategy = "latest",
                ChangeStatus = "changed",
                ResultType = "queued",
                Message = $"发现新版本 {discoveredVersion.CommitSha[..Math.Min(8, discoveredVersion.CommitSha.Length)]}，已提交生成任务"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新版本失败 RepositoryId={RepoId}", request.RepositoryId);
            return new RefreshResult
            {
                RepositoryId = request.RepositoryId,
                RefreshStrategy = request.RefreshStrategy,
                ChangeStatus = "unchanged",
                ResultType = "no_change",
                Message = $"刷新失败：{ex.Message}"
            };
        }
    }
}
