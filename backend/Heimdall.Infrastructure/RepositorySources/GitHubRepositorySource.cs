using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.RepositorySources;

public sealed class GitHubRepositorySource : IRepositorySource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubRepositorySource> _logger;

    public string SourceType => "github";

    public GitHubRepositorySource(HttpClient httpClient, ILogger<GitHubRepositorySource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        url.Contains("github.com", StringComparison.OrdinalIgnoreCase);

    public Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct) =>
        CloneGitRepoAsync(url, targetPath, token, ct);

    public async Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct)
    {
        var (owner, repo) = ParseOwnerRepo(repoUrl);
        var uri = new Uri(repoUrl);
        var apiBase = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com"
            : $"{uri.Scheme}://{uri.Host}/api/v3";
        var requestUri = $"{apiBase}/repos/{owner}/{repo}/contents/{filePath}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Heimdall", "1.0"));
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("token", token);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);
        using var document = System.Text.Json.JsonDocument.Parse(content);
        var encoded = document.RootElement.GetProperty("content").GetString() ?? string.Empty;
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", "", StringComparison.Ordinal)));
    }

    public string NormalizeUrl(string url) =>
        url.TrimEnd('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);

    public (string owner, string repo) ParseOwnerRepo(string url)
    {
        var parts = new Uri(NormalizeUrl(url)).AbsolutePath.Trim('/').Split('/');
        return parts.Length >= 2 ? (parts[^2], parts[^1]) : ("", "");
    }

    private async Task<string> CloneGitRepoAsync(string url, string targetPath, string? token, CancellationToken ct)
    {
        var cloneUrl = url;
        if (!string.IsNullOrWhiteSpace(token))
        {
            var parsed = new Uri(url);
            cloneUrl = $"{parsed.Scheme}://{Uri.EscapeDataString(token)}@{parsed.Authority}{parsed.PathAndQuery}";
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
            var sanitized = SanitizeSecret(errorText, token);
            throw new InvalidOperationException($"克隆仓库失败：{sanitized}");
        }

        return targetPath;
    }

    private static string SanitizeSecret(string text, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return text;
        return text.Replace(secret, "***TOKEN***", StringComparison.Ordinal)
            .Replace(Uri.EscapeDataString(secret), "***TOKEN***", StringComparison.Ordinal);
    }
}
