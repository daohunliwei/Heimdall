namespace Heimdall.Infrastructure.RepositorySources;

/// <summary>
/// 仓库来源抽象，每个平台独立实现。
/// </summary>
public interface IRepositorySource
{
    /// <summary>来源类型标识：github / gitlab / bitbucket / local</summary>
    string SourceType { get; }

    /// <summary>是否可处理给定的 URL</summary>
    bool CanHandle(string url);

    /// <summary>克隆仓库到目标路径，返回本地路径</summary>
    Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct);

    /// <summary>获取单文件内容</summary>
    Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct);

    /// <summary>标准化 URL（去除 .git 后缀、统一协议等）</summary>
    string NormalizeUrl(string url);

    /// <summary>从 URL 解析 owner 和 repo</summary>
    (string owner, string repo) ParseOwnerRepo(string url);
}
