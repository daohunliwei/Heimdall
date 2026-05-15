using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

public class RefreshOrchestrationService : IRefreshOrchestrationService
{
    private readonly IVersionDiscoveryService _versionDiscovery;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IWikiVersionRepository _wikiVersionRepo;
    private readonly ILogger<RefreshOrchestrationService> _logger;

    public RefreshOrchestrationService(
        IVersionDiscoveryService versionDiscovery,
        IRepositoryConfigRepository repoRepo,
        IWikiSpaceRepository spaceRepo,
        IWikiVersionRepository wikiVersionRepo,
        ILogger<RefreshOrchestrationService> logger)
    {
        _versionDiscovery = versionDiscovery;
        _repoRepo = repoRepo;
        _spaceRepo = spaceRepo;
        _wikiVersionRepo = wikiVersionRepo;
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
            var currentWikiVersionId = await ResolveEffectiveWikiVersionIdAsync(request.RepositoryId, request.Language);
            var result = new RefreshResult
            {
                RepositoryId = request.RepositoryId,
                RepositoryVersionId = currentVersion?.Id,
                WikiVersionId = currentWikiVersionId,
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
                var existingWikiVersionId = await ResolveEffectiveWikiVersionIdAsync(request.RepositoryId, request.Language);
                var result = new RefreshResult
                {
                    RepositoryId = request.RepositoryId,
                    RepositoryVersionId = latestVersion.Id,
                    WikiVersionId = existingWikiVersionId,
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
            throw new InvalidOperationException($"刷新失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析指定仓库当前对外可读的 WikiVersion。
    /// 优先返回已发布版本；如果尚未发布，则回退到空间中的最新生成版本。
    /// </summary>
    private async Task<Guid?> ResolveEffectiveWikiVersionIdAsync(Guid repositoryId, string language)
    {
        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, "default");
        if (space is null)
            return null;

        if (space.PublishedWikiVersionId.HasValue)
            return space.PublishedWikiVersionId.Value;

        var versions = await _wikiVersionRepo.GetBySpaceIdAsync(space.Id);
        return versions.OrderByDescending(v => v.VersionNo).FirstOrDefault()?.Id;
    }
}
