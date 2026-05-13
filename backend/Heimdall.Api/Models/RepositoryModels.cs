using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 本地仓库结构响应。
/// </summary>
public sealed class LocalRepoStructureResponse
{
    /// <summary>
    /// 文件树文本。
    /// </summary>
    [JsonPropertyName("file_tree")]
    public string FileTree { get; init; } = string.Empty;

    /// <summary>
    /// README 内容。
    /// </summary>
    [JsonPropertyName("readme")]
    public string Readme { get; init; } = string.Empty;
}

/// <summary>
/// 仓库基础信息。
/// </summary>
public sealed class RepoInfo
{
    /// <summary>
    /// 所有者。
    /// </summary>
    [JsonPropertyName("owner")]
    public string Owner { get; init; } = string.Empty;

    /// <summary>
    /// 仓库名。
    /// </summary>
    [JsonPropertyName("repo")]
    public string Repo { get; init; } = string.Empty;

    /// <summary>
    /// 仓库类型。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "github";

    /// <summary>
    /// 仓库地址。
    /// </summary>
    [JsonPropertyName("repoUrl")]
    public string? RepoUrl { get; init; }

    /// <summary>
    /// 私有仓库令牌。
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>
    /// 本地目录。
    /// </summary>
    [JsonPropertyName("localPath")]
    public string? LocalPath { get; init; }
}

/// <summary>
/// 经过切分和嵌入的仓库文档。
/// </summary>
public sealed class EmbeddedDocument
{
    /// <summary>
    /// 文档标识。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 文档文本。
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径。
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 文件类型。
    /// </summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>
    /// 是否为代码文件。
    /// </summary>
    public bool IsCode { get; init; }

    /// <summary>
    /// 是否为实现文件。
    /// </summary>
    public bool IsImplementation { get; init; }

    /// <summary>
    /// 文本 token 估算值。
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// 向量。
    /// </summary>
    public float[] Vector { get; init; } = Array.Empty<float>();
}

/// <summary>
/// 仓库索引缓存。
/// </summary>
public sealed class RepositoryIndexCache
{
    /// <summary>
    /// 仓库地址或路径。
    /// </summary>
    public string Repository { get; init; } = string.Empty;

    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    public string EmbedderType { get; init; } = "openai";

    /// <summary>
    /// 过滤签名。
    /// </summary>
    public string FilterSignature { get; init; } = string.Empty;

    /// <summary>
    /// 已缓存文档。
    /// </summary>
    public List<EmbeddedDocument> Documents { get; init; } = new();
}
