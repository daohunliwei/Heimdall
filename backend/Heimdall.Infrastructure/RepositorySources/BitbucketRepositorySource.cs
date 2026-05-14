using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.RepositorySources;

public sealed class BitbucketRepositorySource : IRepositorySource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BitbucketRepositorySource> _logger;

    public string SourceType => "bitbucket";

    public BitbucketRepositorySource(HttpClient httpClient, ILogger<BitbucketRepositorySource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        url.Contains("bitbucket", StringComparison.OrdinalIgnoreCase);

    public Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct) =>
        CloneGitRepoAsync(url, targetPath, token, ct);

    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct)
    {
        var (owner, repo) = ParseOwnerRepo(repoUrl);
        var defaultBranch = await GetDefaultBranchAsync(owner, repo, token, ct);
        var fileUrl = $"https://api.bitbucket.org/2.0/repositories/{owner}/{repo}/src/{Uri.EscapeDataString(defaultBranch)}/{filePath}";

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public string NormalizeUrl(string url) =>
        url.TrimEnd('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);

    public (string owner, string repo) ParseOwnerRepo(string url)
    {
        var parts = new Uri(NormalizeUrl(url)).AbsolutePath.Trim('/').Split('/');
        return parts.Length >= 2 ? (parts[^2], parts[^1]) : ("", "");
    }

    private async Task<string> GetDefaultBranchAsync(string owner, string repo, string? token, CancellationToken ct)
    {
        var url = $"https://api.bitbucket.org/2.0/repositories/{owner}/{repo}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("mainbranch", out var mb) &&
                mb.TryGetProperty("name", out var name))
                return name.GetString() ?? "main";
        }

        return "main";
    }

    private async Task<string> CloneGitRepoAsync(string url, string targetPath, string? token, CancellationToken ct)
    {
        var cloneUrl = url;
        if (!string.IsNullOrWhiteSpace(token))
        {
            var parsed = new Uri(url);
            var authPrefix = token.StartsWith("ATCTT", StringComparison.Ordinal)
                ? $"x-bitbucket-api-token-auth:{Uri.EscapeDataString(token)}"
                : $"x-token-auth:{Uri.EscapeDataString(token)}";
            cloneUrl = $"{parsed.Scheme}://{authPrefix}@{parsed.Authority}{parsed.PathAndQuery}";
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
