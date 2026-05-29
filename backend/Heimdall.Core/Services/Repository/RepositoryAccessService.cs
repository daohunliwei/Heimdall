using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.RepositorySources;
using Heimdall.Infrastructure.Services;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

public sealed class RepositoryAccessService
{
    private readonly IEnumerable<IRepositorySource> _sources;
    private readonly TextUtilityService _textUtility;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<RepositoryAccessService> _logger;

    private static readonly string[] CodeExtensions =
        [".py", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs",
         ".jsx", ".tsx", ".html", ".css", ".php", ".swift", ".cs"];
    private static readonly string[] DocExtensions =
        [".md", ".txt", ".rst", ".json", ".yaml", ".yml"];

    public RepositoryAccessService(
        IEnumerable<IRepositorySource> sources,
        TextUtilityService textUtility,
        WorkspaceService workspace,
        ILogger<RepositoryAccessService> logger)
    {
        _sources = sources;
        _textUtility = textUtility;
        _workspace = workspace;
        _logger = logger;
    }

    public async Task<string> PrepareRepositoryAsync(string url, string repoType, string? token, CancellationToken ct)
    {
        if (Directory.Exists(url))
        {
            _logger.LogInformation("直接使用本地目录 Path={Path}", url);
            return url;
        }

        var source = FindSource(repoType, url);
        var normalizedUrl = source.NormalizeUrl(url);
        var (owner, repo) = source.ParseOwnerRepo(normalizedUrl);
        var targetPath = _workspace.GetRepoPath(owner, repo);

        Directory.CreateDirectory(targetPath);
        if (Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            _logger.LogInformation("命中已克隆缓存 Path={Path}", targetPath);
            return targetPath;
        }

        _logger.LogInformation("开始克隆仓库 Type={Type} Url={Url}", repoType, url);
        return await source.CloneAsync(url, targetPath, token, ct);
    }

    public LocalRepoStructureResponse GetLocalStructure(string path)
    {
        var files = new List<string>();
        var readme = string.Empty;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(path, file).Replace('\\', '/');
            if (ShouldSkipPath(relativePath)) continue;
            files.Add(relativePath);
            if (string.IsNullOrEmpty(readme) &&
                string.Equals(Path.GetFileName(file), "README.md", StringComparison.OrdinalIgnoreCase))
                readme = File.ReadAllText(file);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return new LocalRepoStructureResponse { FileTree = string.Join('\n', files), Readme = readme };
    }

    public async Task<string> GetFileContentAsync(string url, string filePath, string repoType, string? token, CancellationToken ct)
    {
        if (Directory.Exists(url))
        {
            var fullPath = Path.Combine(url, filePath);
            return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, ct) : string.Empty;
        }

        var source = FindSource(repoType, url);
        return await source.GetFileContentAsync(url, filePath, token, ct);
    }

    public async Task<List<EmbeddedDocument>> ReadRepositoryDocumentsAsync(
        string path, List<string> excludedDirs, List<string> excludedFiles,
        List<string> includedDirs, List<string> includedFiles, CancellationToken ct)
    {
        var useInclusion = includedDirs.Count > 0 || includedFiles.Count > 0;
        var extensions = CodeExtensions.Concat(DocExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var documents = new List<EmbeddedDocument>();

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file);
            if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

            var relativePath = Path.GetRelativePath(path, file).Replace('\\', '/');
            if (ShouldSkipPath(relativePath)) continue;

            var content = await File.ReadAllTextAsync(file, ct);
            var tokenCount = _textUtility.EstimateTokenCount(content);
            var isCode = CodeExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            if ((isCode && tokenCount > 81920) || (!isCode && tokenCount > 8192)) continue;

            documents.Add(new EmbeddedDocument
            {
                FilePath = relativePath,
                FileType = ext.Trim('.'),
                IsCode = isCode,
                IsImplementation = isCode && !relativePath.Contains("test", StringComparison.OrdinalIgnoreCase),
                Text = content,
                TokenCount = tokenCount
            });
        }

        return documents;
    }

    public IRepositorySource FindSource(string repoType, string url)
    {
        return _sources.FirstOrDefault(s => s.CanHandle(url) || s.SourceType == repoType.ToLowerInvariant())
            ?? _sources.First(s => s.SourceType == "local");
    }

    private static bool ShouldSkipPath(string relativePath) =>
        relativePath.Contains("/.git/") ||
        relativePath.Contains("/node_modules/") ||
        relativePath.Contains("/__pycache__/") ||
        relativePath.StartsWith('.');
}
