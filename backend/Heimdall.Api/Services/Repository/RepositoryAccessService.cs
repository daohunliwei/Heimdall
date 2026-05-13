using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Repository;

/// <summary>
/// 仓库访问服务，负责仓库下载、本地目录读取以及单文件内容获取。
/// </summary>
public sealed class RepositoryAccessService
{
    private const string HeimdallStorageDirKey = "HEIMDALL_STORAGE_DIR";
    private static readonly string[] CodeExtensions =
    [
        ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs",
        ".jsx", ".tsx", ".html", ".css", ".php", ".swift", ".cs"
    ];

    private static readonly string[] DocumentationExtensions =
    [
        ".md", ".txt", ".rst", ".json", ".yaml", ".yml"
    ];

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<RepositoryAccessService> _logger;
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化仓库访问服务。
    /// </summary>
    public RepositoryAccessService(
        IConfiguration configuration,
        HttpClient httpClient,
        HeimdallConfigService configService,
        ILogger<RepositoryAccessService> logger,
        TextUtilityService textUtilityService)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 获取本地仓库结构。
    /// </summary>
    public LocalRepoStructureResponse GetLocalStructure(string path)
    {
        var files = new List<string>();
        var readme = string.Empty;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(path, file).Replace('\\', '/');
            if (ShouldSkipPath(relativePath))
            {
                continue;
            }

            files.Add(relativePath);
            if (string.IsNullOrEmpty(readme) && string.Equals(Path.GetFileName(file), "README.md", StringComparison.OrdinalIgnoreCase))
            {
                readme = File.ReadAllText(file);
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return new LocalRepoStructureResponse
        {
            FileTree = string.Join('\n', files),
            Readme = readme
        };
    }

    /// <summary>
    /// 解析仓库到本地目录。
    /// </summary>
    public async Task<string> PrepareRepositoryAsync(string repoUrlOrPath, string repoType, string? accessToken, CancellationToken cancellationToken)
    {
        if (Directory.Exists(repoUrlOrPath))
        {
            _logger.LogInformation("直接使用本地仓库目录 Path={RepositoryPath}", repoUrlOrPath);
            return repoUrlOrPath;
        }

        var repoName = ExtractRepoStorageName(repoUrlOrPath, repoType);
        var rootPath = GetStorageRootPath();
        var repoDirectory = Path.Combine(rootPath, "repos", repoName);
        Directory.CreateDirectory(Path.GetDirectoryName(repoDirectory)!);

        if (Directory.Exists(repoDirectory) && Directory.EnumerateFileSystemEntries(repoDirectory).Any())
        {
            _logger.LogInformation("命中已克隆仓库缓存 RepoType={RepoType} RepoDirectory={RepoDirectory}", repoType, repoDirectory);
            return repoDirectory;
        }

        if (Directory.Exists(repoDirectory))
        {
            Directory.Delete(repoDirectory, true);
        }

        Directory.CreateDirectory(repoDirectory);
        var cloneUrl = BuildCloneUrl(repoUrlOrPath, repoType, accessToken);
        _logger.LogInformation("开始克隆仓库 RepoType={RepoType} RepoDirectory={RepoDirectory} Source={Source}", repoType, repoDirectory, SanitizeSecret(repoUrlOrPath, accessToken));
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--depth=1");
        startInfo.ArgumentList.Add("--single-branch");
        startInfo.ArgumentList.Add(cloneUrl);
        startInfo.ArgumentList.Add(repoDirectory);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 git clone 进程。");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var errorText = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogWarning("克隆仓库失败 RepoType={RepoType} RepoDirectory={RepoDirectory} Error={Error}", repoType, repoDirectory, SanitizeSecret(errorText, accessToken));
            throw new InvalidOperationException($"克隆仓库失败：{SanitizeSecret(errorText, accessToken)}");
        }

        _logger.LogInformation("克隆仓库完成 RepoType={RepoType} RepoDirectory={RepoDirectory}", repoType, repoDirectory);
        return repoDirectory;
    }

    /// <summary>
    /// 读取并过滤仓库文件，产出原始文档集合。
    /// </summary>
    public async Task<List<EmbeddedDocument>> ReadRepositoryDocumentsAsync(
        string repositoryPath,
        string embedderType,
        List<string> excludedDirs,
        List<string> excludedFiles,
        List<string> includedDirs,
        List<string> includedFiles,
        CancellationToken cancellationToken)
    {
        var useInclusionMode = includedDirs.Count > 0 || includedFiles.Count > 0;
        var repoConfig = _configService.GetRepoConfig();

        if (!useInclusionMode)
        {
            excludedDirs = repoConfig.FileFilters.ExcludedDirs.Concat(excludedDirs).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            excludedFiles = repoConfig.FileFilters.ExcludedFiles.Concat(excludedFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        var documents = new List<EmbeddedDocument>();
        var extensions = CodeExtensions.Concat(DocumentationExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var file in Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ShouldProcessFile(file, useInclusionMode, includedDirs, includedFiles, excludedDirs, excludedFiles))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(repositoryPath, file).Replace('\\', '/');
            var tokenCount = _textUtilityService.EstimateTokenCount(content);
            var isCode = CodeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
            if ((isCode && tokenCount > 8192 * 10) || (!isCode && tokenCount > 8192))
            {
                continue;
            }

            documents.Add(new EmbeddedDocument
            {
                FilePath = relativePath,
                FileType = extension.Trim('.'),
                IsCode = isCode,
                IsImplementation = isCode && !relativePath.Contains("test", StringComparison.OrdinalIgnoreCase),
                Text = content,
                TokenCount = tokenCount
            });
        }

        return documents;
    }

    /// <summary>
    /// 按仓库类型读取单文件内容。
    /// </summary>
    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, string repoType, string? accessToken, CancellationToken cancellationToken)
    {
        if (Directory.Exists(repoUrl))
        {
            var fullPath = Path.Combine(repoUrl, filePath);
            return File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, cancellationToken) : string.Empty;
        }

        return repoType switch
        {
            "gitlab" => await GetGitLabFileContentAsync(repoUrl, filePath, accessToken, cancellationToken),
            "bitbucket" => await GetBitbucketFileContentAsync(repoUrl, filePath, accessToken, cancellationToken),
            _ => await GetGitHubFileContentAsync(repoUrl, filePath, accessToken, cancellationToken)
        };
    }

    /// <summary>
    /// 获取仓库数据库缓存目录。
    /// </summary>
    public string GetRepositoryDatabaseCachePath(string repoUrlOrPath, string repoType, string embedderType, string filterSignature)
    {
        var rootPath = GetStorageRootPath();
        var repoName = ExtractRepoStorageName(repoUrlOrPath, repoType);
        return Path.Combine(rootPath, "databases", $"{repoName}_{embedderType}_{filterSignature}.json");
    }

    /// <summary>
    /// 判断路径是否应该跳过。
    /// </summary>
    private static bool ShouldSkipPath(string relativePath)
    {
        return relativePath.Contains("/.git/")
            || relativePath.Contains("/node_modules/")
            || relativePath.Contains("/__pycache__/")
            || relativePath.Contains("/.venv/")
            || relativePath.StartsWith(".", StringComparison.Ordinal)
            || relativePath.EndsWith("/__init__.py", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断文件是否应该处理。
    /// </summary>
    private static bool ShouldProcessFile(
        string filePath,
        bool useInclusionMode,
        List<string> includedDirs,
        List<string> includedFiles,
        List<string> excludedDirs,
        List<string> excludedFiles)
    {
        var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (useInclusionMode)
        {
            var inIncludedDir = includedDirs.Any(included =>
            {
                var clean = included.Trim().TrimStart('.').Trim('/').Replace('\\', '/');
                return segments.Contains(clean, StringComparer.OrdinalIgnoreCase) || normalizedPath.Contains($"/{clean}/", StringComparison.OrdinalIgnoreCase);
            });
            var inIncludedFile = includedFiles.Any(included => fileName.Equals(included, StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(included, StringComparison.OrdinalIgnoreCase));
            return (includedDirs.Count == 0 && includedFiles.Count == 0) || inIncludedDir || inIncludedFile;
        }

        var inExcludedDir = excludedDirs.Any(excluded =>
        {
            var clean = excluded.Trim().TrimStart('.').Trim('/').Replace('\\', '/');
            return segments.Contains(clean, StringComparer.OrdinalIgnoreCase) || normalizedPath.Contains($"/{clean}/", StringComparison.OrdinalIgnoreCase);
        });
        if (inExcludedDir)
        {
            return false;
        }

        return !excludedFiles.Any(excluded => MatchesPattern(fileName, excluded));
    }

    /// <summary>
    /// 进行简单通配符匹配。
    /// </summary>
    private static bool MatchesPattern(string input, string pattern)
    {
        if (!pattern.Contains('*'))
        {
            return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
        }

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(input, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 构建带鉴权信息的 clone URL。
    /// </summary>
    private static string BuildCloneUrl(string repoUrl, string repoType, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return repoUrl;
        }

        var parsed = new Uri(repoUrl);
        var encodedToken = Uri.EscapeDataString(accessToken);
        var authority = repoType switch
        {
            "gitlab" => $"oauth2:{encodedToken}@{parsed.Authority}",
            "bitbucket" when accessToken.StartsWith("ATCTT", StringComparison.Ordinal) => $"x-bitbucket-api-token-auth:{encodedToken}@{parsed.Authority}",
            "bitbucket" => $"x-token-auth:{encodedToken}@{parsed.Authority}",
            _ => $"{encodedToken}@{parsed.Authority}"
        };

        return $"{parsed.Scheme}://{authority}{parsed.PathAndQuery}";
    }

    /// <summary>
    /// 计算仓库存储名。
    /// </summary>
    private string ExtractRepoStorageName(string repoUrlOrPath, string repoType)
    {
        if (Directory.Exists(repoUrlOrPath))
        {
            return Path.GetFileName(repoUrlOrPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        var urlParts = repoUrlOrPath.TrimEnd('/').Split('/');
        if (new[] { "github", "gitlab", "bitbucket" }.Contains(repoType) && urlParts.Length >= 2)
        {
            return $"{urlParts[^2]}_{urlParts[^1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase)}";
        }

        return _textUtilityService.ExtractRepositoryName(repoUrlOrPath);
    }

    /// <summary>
    /// 拉取 GitHub 文件内容。
    /// </summary>
    private async Task<string> GetGitHubFileContentAsync(string repoUrl, string filePath, string? accessToken, CancellationToken cancellationToken)
    {
        var uri = new Uri(repoUrl);
        var pathParts = uri.AbsolutePath.Trim('/').Split('/');
        var owner = pathParts[^2];
        var repo = pathParts[^1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);
        var apiBase = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com"
            : $"{uri.Scheme}://{uri.Host}/api/v3";
        var requestUri = $"{apiBase}/repos/{owner}/{repo}/contents/{filePath}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Heimdall", "1.0"));
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("token", accessToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(content);
        var encoded = document.RootElement.GetProperty("content").GetString() ?? string.Empty;
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 拉取 GitLab 文件内容。
    /// </summary>
    private async Task<string> GetGitLabFileContentAsync(string repoUrl, string filePath, string? accessToken, CancellationToken cancellationToken)
    {
        var uri = new Uri(repoUrl);
        var projectPath = uri.AbsolutePath.Trim('/').Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);
        var encodedProject = Uri.EscapeDataString(projectPath);
        var encodedFile = Uri.EscapeDataString(filePath);
        var projectInfoUrl = $"{uri.Scheme}://{uri.Authority}/api/v4/projects/{encodedProject}";
        var defaultBranch = "main";

        using (var projectRequest = new HttpRequestMessage(HttpMethod.Get, projectInfoUrl))
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                projectRequest.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", accessToken);
            }

            using var projectResponse = await _httpClient.SendAsync(projectRequest, cancellationToken);
            if (projectResponse.IsSuccessStatusCode)
            {
                var projectText = await projectResponse.Content.ReadAsStringAsync(cancellationToken);
                using var projectDocument = System.Text.Json.JsonDocument.Parse(projectText);
                defaultBranch = projectDocument.RootElement.TryGetProperty("default_branch", out var branchElement)
                    ? (branchElement.GetString() ?? "main")
                    : "main";
            }
        }

        var fileUrl = $"{uri.Scheme}://{uri.Authority}/api/v4/projects/{encodedProject}/repository/files/{encodedFile}/raw?ref={Uri.EscapeDataString(defaultBranch)}";
        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            fileRequest.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", accessToken);
        }

        using var response = await _httpClient.SendAsync(fileRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// 拉取 Bitbucket 文件内容。
    /// </summary>
    private async Task<string> GetBitbucketFileContentAsync(string repoUrl, string filePath, string? accessToken, CancellationToken cancellationToken)
    {
        var uri = new Uri(repoUrl);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        var owner = parts[^2];
        var repo = parts[^1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);
        var repoInfoUrl = $"https://api.bitbucket.org/2.0/repositories/{owner}/{repo}";
        var defaultBranch = "main";

        using (var repoRequest = new HttpRequestMessage(HttpMethod.Get, repoInfoUrl))
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                repoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var repoResponse = await _httpClient.SendAsync(repoRequest, cancellationToken);
            if (repoResponse.IsSuccessStatusCode)
            {
                var repoText = await repoResponse.Content.ReadAsStringAsync(cancellationToken);
                using var repoDocument = System.Text.Json.JsonDocument.Parse(repoText);
                if (repoDocument.RootElement.TryGetProperty("mainbranch", out var mainBranch) &&
                    mainBranch.TryGetProperty("name", out var nameElement))
                {
                    defaultBranch = nameElement.GetString() ?? "main";
                }
            }
        }

        var fileUrl = $"https://api.bitbucket.org/2.0/repositories/{owner}/{repo}/src/{Uri.EscapeDataString(defaultBranch)}/{filePath}";
        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            fileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await _httpClient.SendAsync(fileRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// 获取仓库存储根目录。
    /// </summary>
    private string GetStorageRootPath()
    {
        var rootPath = _configuration[HeimdallStorageDirKey];
        return !string.IsNullOrWhiteSpace(rootPath)
            ? rootPath
            : Path.Combine(AppContext.BaseDirectory, "storage");
    }

    /// <summary>
    /// 清理错误信息中的敏感令牌。
    /// </summary>
    private static string SanitizeSecret(string text, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return text;
        }

        return text.Replace(secret, "***TOKEN***", StringComparison.Ordinal)
            .Replace(Uri.EscapeDataString(secret), "***TOKEN***", StringComparison.Ordinal);
    }
}
