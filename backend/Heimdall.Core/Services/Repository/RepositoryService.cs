using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Core.Services.Repository;

public class RepositoryService : IRepositoryService
{
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly ILogger<RepositoryService> _logger;

    public RepositoryService(
        IRepositoryConfigRepository repoRepo,
        ILogger<RepositoryService> logger)
    {
        _repoRepo = repoRepo;
        _logger = logger;
    }

    public async Task<RepositoryEntity> ImportAsync(string repoUrl, CancellationToken cancellationToken = default)
    {
        var (providerType, owner, repoName, cloneUrl) = ParseRepoUrl(repoUrl);
        var displayName = $"{owner}/{repoName}";

        // 尝试按 provider_type + provider_repository_key 查找
        var providerKey = BuildProviderKey(repoUrl);
        var existing = await _repoRepo.GetByProviderKeyAsync(providerType, providerKey);

        if (existing is not null)
        {
            _logger.LogInformation("仓库已存在，复用记录 RepositoryId={Id}", existing.Id);
            // 更新展示名称（owner/repo 可能变更）
            if (existing.DisplayName != displayName)
            {
                existing.DisplayName = displayName;
                await _repoRepo.UpdateAsync(existing);
            }
            return existing;
        }

        // 按 owner+repoName+repoType 兜底查找
        existing = await _repoRepo.GetByOwnerRepoTypeAsync(owner, repoName, providerType);
        if (existing is not null)
        {
            existing.ProviderRepositoryKey = providerKey;
            if (existing.DisplayName != displayName)
                existing.DisplayName = displayName;
            await _repoRepo.UpdateAsync(existing);
            _logger.LogInformation("仓库已存在（兜底匹配），更新 providerKey RepositoryId={Id}", existing.Id);
            return existing;
        }

        var repository = new RepositoryEntity
        {
            ProviderType = providerType,
            ProviderRepositoryKey = providerKey,
            DisplayName = displayName,
            Owner = owner,
            RepoName = repoName,
            RepoType = providerType,
            RepoUrl = repoUrl,
            CloneUrl = cloneUrl,
            DefaultBranch = "main",
            DefaultLanguage = "zh"
        };

        var created = await _repoRepo.AddAsync(repository);
        _logger.LogInformation("新建仓库记录 RepositoryId={Id} DisplayName={DisplayName}", created.Id, displayName);
        return created;
    }

    public Task<List<RepositoryEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repoRepo.GetAllAsync();
    }

    public Task<RepositoryEntity?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        return _repoRepo.GetByIdAsync(repositoryId);
    }

    public async Task<RepositoryEntity?> UpdateAsync(Guid repositoryId, Action<RepositoryEntity> patch, CancellationToken cancellationToken = default)
    {
        var entity = await _repoRepo.GetByIdAsync(repositoryId);
        if (entity is null) return null;
        patch(entity);
        return await _repoRepo.UpdateAsync(entity);
    }

    /// <summary>
    /// 删除仓库及其关联数据。V4：数据库外键级联删除自动处理 WikiSpace/WikiVersion/WikiPage 清理。
    /// </summary>
    public async Task<bool> DeleteAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        // 数据库外键 CASCADE 自动清理 WikiSpace → WikiVersion → WikiPage 链路
        // 无需手动清理旧 Wiki 实体
        return await _repoRepo.DeleteAsync(repositoryId);
    }

    private static (string providerType, string owner, string repoName, string cloneUrl) ParseRepoUrl(string url)
    {
        url = url.Trim();
        string providerType = "github";
        string owner = "";
        string repoName = "";
        string cloneUrl = url;

        // 本地路径
        if (System.IO.Path.IsPathFullyQualified(url) || url.StartsWith('/'))
        {
            providerType = "local";
            repoName = System.IO.Path.GetFileName(url.TrimEnd('/', '\\'));
            owner = "local";
            cloneUrl = url;
            return (providerType, owner, repoName, cloneUrl);
        }

        // 移除 .git 后缀
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        // 解析 Git 托管平台
        var uri = new Uri(url);
        if (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            providerType = "github";
        else if (uri.Host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
            providerType = "gitlab";
        else if (uri.Host.Contains("bitbucket", StringComparison.OrdinalIgnoreCase))
            providerType = "bitbucket";

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length >= 2)
        {
            owner = segments[^2];
            repoName = segments[^1];
        }

        cloneUrl = url + ".git";
        return (providerType, owner, repoName, cloneUrl);
    }

    private static string BuildProviderKey(string url)
    {
        // 使用 URL 路径作为稳定的 provider_key
        url = url.Trim();
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        if (System.IO.Path.IsPathFullyQualified(url) || url.StartsWith('/'))
            return $"local:{url.Replace('\\', '/')}";

        var uri = new Uri(url);
        return $"{uri.Host}{uri.AbsolutePath}".TrimEnd('/');
    }
}
