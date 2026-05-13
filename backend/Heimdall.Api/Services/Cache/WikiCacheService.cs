using System.Text.Json;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Tasks;

namespace Heimdall.Api.Services.Cache;

/// <summary>
/// 负责 Wiki 缓存文件的读取、保存与删除。
/// </summary>
public sealed class WikiCacheService
{
    private const string CacheFilePrefixValue = "heimdall_cache_";
    private const string HeimdallDataDirKey = "HEIMDALL_DATA_DIR";
    private readonly string _cacheDirectory;
    private readonly HeimdallConfigService _configService;

    /// <summary>
    /// 初始化缓存服务。
    /// </summary>
    public WikiCacheService(IConfiguration configuration, HeimdallConfigService configService)
    {
        _configService = configService;
        var dataRoot = configuration[HeimdallDataDirKey] ?? Path.Combine(AppContext.BaseDirectory, "data");
        _cacheDirectory = Path.Combine(dataRoot, "wikicache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// 获取缓存目录绝对路径。
    /// </summary>
    public string CacheDirectoryPath => _cacheDirectory;

    /// <summary>
    /// 获取缓存文件名前缀。
    /// </summary>
    public string CacheFilePrefix => CacheFilePrefixValue;

    /// <summary>
    /// 读取指定缓存。
    /// </summary>
    public async Task<WikiCacheData?> GetAsync(string owner, string repo, string repoType, string language)
    {
        var cachePath = BuildCachePath(owner, repo, repoType, NormalizeLanguage(language));
        if (!File.Exists(cachePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(cachePath);
        var cacheData = JsonSerializer.Deserialize<WikiCacheData>(content);
        if (cacheData is null)
        {
            return null;
        }

        return new WikiCacheData
        {
            RepoUrl = cacheData.RepoUrl,
            Repo = cacheData.Repo,
            Provider = cacheData.Provider,
            Model = cacheData.Model,
            Language = cacheData.Language,
            WikiStructure = cacheData.WikiStructure,
            GeneratedPages = WikiMarkdownNormalizer.NormalizePages(cacheData.GeneratedPages)
        };
    }

    /// <summary>
    /// 保存缓存。
    /// </summary>
    public async Task SaveAsync(WikiCacheSaveRequest request)
    {
        var normalizedLanguage = NormalizeLanguage(request.Language);
        var sanitizedRepo = new RepoInfo
        {
            Owner = request.Repo.Owner,
            Repo = request.Repo.Repo,
            Type = request.Repo.Type,
            RepoUrl = request.Repo.RepoUrl,
            Token = null,
            LocalPath = request.Repo.LocalPath
        };
        var cacheData = new WikiCacheData
        {
            RepoUrl = sanitizedRepo.RepoUrl,
            Repo = sanitizedRepo,
            Provider = request.Provider,
            Model = request.Model,
            Language = normalizedLanguage,
            WikiStructure = request.WikiStructure,
            GeneratedPages = WikiMarkdownNormalizer.NormalizePages(request.GeneratedPages)
        };

        var cachePath = BuildCachePath(sanitizedRepo.Owner, sanitizedRepo.Repo, sanitizedRepo.Type, normalizedLanguage);
        var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(cachePath, json);
    }

    /// <summary>
    /// 删除缓存。
    /// </summary>
    public Task<bool> DeleteAsync(string owner, string repo, string repoType, string language)
    {
        var cachePath = BuildCachePath(owner, repo, repoType, NormalizeLanguage(language));
        if (!File.Exists(cachePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(cachePath);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 归一化语言值，保证与配置一致。
    /// </summary>
    private string NormalizeLanguage(string? language)
    {
        var languageConfig = _configService.GetLanguageConfig();
        if (string.IsNullOrWhiteSpace(language))
        {
            return languageConfig.Default;
        }

        var normalized = language.Trim();
        if (languageConfig.SupportedLanguages.Count == 0)
        {
            return normalized;
        }

        return languageConfig.SupportedLanguages.ContainsKey(normalized)
            ? normalized
            : languageConfig.Default;
    }

    /// <summary>
    /// 计算缓存文件路径。
    /// </summary>
    private string BuildCachePath(string owner, string repo, string repoType, string language)
    {
        return Path.Combine(_cacheDirectory, $"{CacheFilePrefixValue}{repoType}_{owner}_{repo}_{language}.json");
    }
}
