using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// Wiki 页面定义。
/// </summary>
public sealed class WikiPage
{
    /// <summary>
    /// 页面标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 页面标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 页面简介。
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 页面正文。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径列表。
    /// </summary>
    [JsonPropertyName("filePaths")]
    public List<string> FilePaths { get; init; } = new();

    /// <summary>
    /// 重要级别。
    /// </summary>
    [JsonPropertyName("importance")]
    public string Importance { get; init; } = "medium";

    /// <summary>
    /// 相关页面列表。
    /// </summary>
    [JsonPropertyName("relatedPages")]
    public List<string> RelatedPages { get; init; } = new();

    /// <summary>
    /// 父级分区标识。
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }

    /// <summary>
    /// 是否为分区节点。
    /// </summary>
    [JsonPropertyName("isSection")]
    public bool? IsSection { get; init; }

    /// <summary>
    /// 子节点列表。
    /// </summary>
    [JsonPropertyName("children")]
    public List<string>? Children { get; init; }
}

/// <summary>
/// Wiki 分区定义。
/// </summary>
public sealed class WikiSection
{
    /// <summary>
    /// 分区标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 分区标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 页面引用。
    /// </summary>
    [JsonPropertyName("pages")]
    public List<string> Pages { get; init; } = new();

    /// <summary>
    /// 子分区引用。
    /// </summary>
    [JsonPropertyName("subsections")]
    public List<string>? Subsections { get; init; }
}

/// <summary>
/// Wiki 结构定义。
/// </summary>
public sealed class WikiStructure
{
    /// <summary>
    /// 结构标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 描述。
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 页面列表。
    /// </summary>
    [JsonPropertyName("pages")]
    public List<WikiPage> Pages { get; init; } = new();

    /// <summary>
    /// 分区列表。
    /// </summary>
    [JsonPropertyName("sections")]
    public List<WikiSection> Sections { get; init; } = new();

    /// <summary>
    /// 根分区列表。
    /// </summary>
    [JsonPropertyName("rootSections")]
    public List<string> RootSections { get; init; } = new();
}

/// <summary>
/// Wiki 导出请求。
/// </summary>
public sealed class WikiExportRequest
{
    /// <summary>
    /// 仓库地址。
    /// </summary>
    [JsonPropertyName("repo_url")]
    public string RepoUrl { get; init; } = string.Empty;

    /// <summary>
    /// 导出格式。
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; init; } = "markdown";

    /// <summary>
    /// 页面列表。
    /// </summary>
    [JsonPropertyName("pages")]
    public List<WikiPage> Pages { get; init; } = new();
}
