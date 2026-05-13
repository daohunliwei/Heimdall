using Heimdall.Api.Models;
using Heimdall.Api.Services.Cache;

namespace Heimdall.Api.Services.Projects;

/// <summary>
/// 负责聚合已生成缓存的项目列表。
/// </summary>
public sealed class ProcessedProjectService
{
    private readonly WikiCacheService _wikiCacheService;

    /// <summary>
    /// 初始化项目服务。
    /// </summary>
    public ProcessedProjectService(WikiCacheService wikiCacheService)
    {
        _wikiCacheService = wikiCacheService;
    }

    /// <summary>
    /// 获取已处理项目列表，并按最新更新时间倒序返回。
    /// </summary>
    public IReadOnlyCollection<ProcessedProjectEntry> GetProcessedProjects()
    {
        var result = new List<ProcessedProjectEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_wikiCacheService.CacheDirectoryPath))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(
                     _wikiCacheService.CacheDirectoryPath,
                     $"{_wikiCacheService.CacheFilePrefix}*.json",
                     SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.StartsWith(_wikiCacheService.CacheFilePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var data = fileName[_wikiCacheService.CacheFilePrefix.Length..];
            var parts = data.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var repoType = parts[0];
            var owner = parts[1];
            var language = parts[^1];
            var repo = string.Join("_", parts.Skip(2).Take(parts.Length - 3));
            var submittedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds();
            var identity = $"{repoType}:{owner}:{repo}:{language}";
            if (!seenIds.Add(identity))
            {
                continue;
            }

            result.Add(new ProcessedProjectEntry
            {
                Id = Path.GetFileName(file),
                Owner = owner,
                Repo = repo,
                Name = $"{owner}/{repo}",
                RepoType = repoType,
                SubmittedAt = submittedAt,
                Language = language
            });
        }

        return result.OrderByDescending(item => item.SubmittedAt).ToList();
    }
}
