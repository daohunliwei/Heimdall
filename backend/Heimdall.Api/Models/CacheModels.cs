using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 缓存保存请求。
/// </summary>
public sealed class WikiCacheSaveRequest
{
    /// <summary>
    /// 仓库信息。
    /// </summary>
    [JsonPropertyName("repo")]
    public RepoInfo Repo { get; init; } = new();

    /// <summary>
    /// 输出语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; init; } = "zh";

    /// <summary>
    /// Wiki 结构。
    /// </summary>
    [JsonPropertyName("wiki_structure")]
    public WikiStructure WikiStructure { get; init; } = new();

    /// <summary>
    /// 已生成页面。
    /// </summary>
    [JsonPropertyName("generated_pages")]
    public Dictionary<string, WikiPage> GeneratedPages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 模型提供方。
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// 模型名称。
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

/// <summary>
/// 缓存数据。
/// </summary>
public sealed class WikiCacheData
{
    /// <summary>
    /// Wiki 结构。
    /// </summary>
    [JsonPropertyName("wiki_structure")]
    public WikiStructure WikiStructure { get; init; } = new();

    /// <summary>
    /// 已生成页面。
    /// </summary>
    [JsonPropertyName("generated_pages")]
    public Dictionary<string, WikiPage> GeneratedPages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 兼容旧结构的仓库地址。
    /// </summary>
    [JsonPropertyName("repo_url")]
    public string? RepoUrl { get; init; }

    /// <summary>
    /// 仓库对象。
    /// </summary>
    [JsonPropertyName("repo")]
    public RepoInfo? Repo { get; init; }

    /// <summary>
    /// 提供方。
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// 模型。
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// 语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; init; } = "zh";
}

/// <summary>
/// 已处理项目列表项。
/// </summary>
public sealed class ProcessedProjectEntry
{
    /// <summary>
    /// 缓存标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

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
    /// 展示名。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 仓库类型。
    /// </summary>
    [JsonPropertyName("repo_type")]
    public string RepoType { get; init; } = "github";

    /// <summary>
    /// 提交时间。
    /// </summary>
    [JsonPropertyName("submittedAt")]
    public long SubmittedAt { get; init; }

    /// <summary>
    /// 缓存语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; init; } = "zh";
}
