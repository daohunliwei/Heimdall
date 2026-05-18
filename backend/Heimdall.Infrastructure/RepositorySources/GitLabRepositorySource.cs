using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.RepositorySources;

public sealed class GitLabRepositorySource : IRepositorySource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabRepositorySource> _logger;

    public string SourceType => "gitlab";

    public GitLabRepositorySource(HttpClient httpClient, ILogger<GitLabRepositorySource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        url.Contains("gitlab", StringComparison.OrdinalIgnoreCase);

    public Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct) =>
        CloneGitRepoAsync(url, targetPath, token, ct);

    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct)
    {
        var uri = new Uri(repoUrl);
        var projectPath = uri.AbsolutePath.Trim('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);
        var encodedProject = Uri.EscapeDataString(projectPath);
        var defaultBranch = await GetDefaultBranchAsync(uri, encodedProject, token, ct);
        var encodedFile = Uri.EscapeDataString(filePath);
        var fileUrl = $"{uri.Scheme}://{uri.Authority}/api/v4/projects/{encodedProject}/repository/files/{encodedFile}/raw?ref={Uri.EscapeDataString(defaultBranch)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", token);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public string NormalizeUrl(string url) =>
        url.TrimEnd('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);

    public (string owner, string repo) ParseOwnerRepo(string url)
    {
        var uri = new Uri(NormalizeUrl(url));
        // GitLab URL 格式: /group/subgroup/.../project
        var path = uri.AbsolutePath.Trim('/');
        var lastSegment = path.Split('/').Last();
        var ownerPath = path[..^(lastSegment.Length + 1)];
        return (ownerPath, lastSegment);
    }

    private async Task<string> GetDefaultBranchAsync(Uri uri, string encodedProject, string? token, CancellationToken ct)
    {
        var infoUrl = $"{uri.Scheme}://{uri.Authority}/api/v4/projects/{encodedProject}";
        using var request = new HttpRequestMessage(HttpMethod.Get, infoUrl);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("default_branch", out var el)
                ? (el.GetString() ?? "main") : "main";
        }

        return "main";
    }

    private async Task<string> CloneGitRepoAsync(string url, string targetPath, string? token, CancellationToken ct)
    {
        var cloneUrl = url;
        if (!string.IsNullOrWhiteSpace(token))
        {
            var parsed = new Uri(url);
            cloneUrl = $"{parsed.Scheme}://oauth2:{Uri.EscapeDataString(token)}@{parsed.Authority}{parsed.PathAndQuery}";
        }

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
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 git clone 进程。");
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            var errorText = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"克隆仓库失败：{errorText}");
        }

        return targetPath;
    }
}
