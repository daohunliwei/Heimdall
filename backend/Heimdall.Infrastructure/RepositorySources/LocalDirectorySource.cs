using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.RepositorySources;

public sealed class LocalDirectorySource : IRepositorySource
{
    private readonly ILogger<LocalDirectorySource> _logger;

    public string SourceType => "local";

    public LocalDirectorySource(ILogger<LocalDirectorySource> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        Directory.Exists(url);

    public Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct)
    {
        _logger.LogInformation("直接使用本地仓库目录 Path={Path}", url);
        return Task.FromResult(url);
    }

    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct)
    {
        var fullPath = Path.Combine(repoUrl, filePath);
        return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, ct) : string.Empty;
    }

    public string NormalizeUrl(string url) => url;

    public (string owner, string repo) ParseOwnerRepo(string url) =>
        ("local", new DirectoryInfo(url).Name);
}
