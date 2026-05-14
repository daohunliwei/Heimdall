using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

public sealed class WikiPageDto
{
    /// <summary>
    /// 页面标识。
    /// </summary>
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 页面标题。
    /// </summary>
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 页面描述。
    /// </summary>
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 页面 Markdown 内容。
    /// </summary>
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 导航标题。
    /// </summary>
    [JsonPropertyName("navTitle")] public string NavTitle { get; init; } = string.Empty;

    /// <summary>
    /// 页面类型。
    /// </summary>
    [JsonPropertyName("pageType")] public string PageType { get; init; } = "article";

    /// <summary>
    /// 源文件路径列表。
    /// </summary>
    [JsonPropertyName("filePaths")] public List<string> FilePaths { get; init; } = new();

    /// <summary>
    /// 页面重要性。
    /// </summary>
    [JsonPropertyName("importance")] public string Importance { get; init; } = "medium";

    /// <summary>
    /// 关联页面列表。
    /// </summary>
    [JsonPropertyName("relatedPages")] public List<string> RelatedPages { get; init; } = new();

    /// <summary>
    /// 前置阅读页面列表。
    /// </summary>
    [JsonPropertyName("prerequisitePages")] public List<string> PrerequisitePages { get; init; } = new();

    /// <summary>
    /// 父页面标识。
    /// </summary>
    [JsonPropertyName("parentId")] public string? ParentId { get; init; }

    /// <summary>
    /// 是否为目录型页面。
    /// </summary>
    [JsonPropertyName("isSection")] public bool? IsSection { get; init; }

    /// <summary>
    /// 子页面标识列表。
    /// </summary>
    [JsonPropertyName("children")] public List<string>? Children { get; init; }

    /// <summary>
    /// Frontmatter 元数据。
    /// </summary>
    [JsonPropertyName("frontMatter")] public WikiPageFrontMatterDto FrontMatter { get; init; } = new();

    /// <summary>
    /// 页面目录提纲。
    /// </summary>
    [JsonPropertyName("outline")] public List<WikiPageHeadingDto> Outline { get; init; } = new();

    /// <summary>
    /// 源码覆盖信息。
    /// </summary>
    [JsonPropertyName("sourceCoverage")] public WikiPageSourceCoverageDto SourceCoverage { get; init; } = new();

    /// <summary>
    /// 页面告警列表。
    /// </summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// 是否使用兜底草案。
    /// </summary>
    [JsonPropertyName("isFallbackDraft")] public bool IsFallbackDraft { get; init; }
}

/// <summary>
/// 页面 Frontmatter DTO。
/// </summary>
public sealed class WikiPageFrontMatterDto
{
    /// <summary>
    /// 摘要。
    /// </summary>
    [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// 描述。
    /// </summary>
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 标签列表。
    /// </summary>
    [JsonPropertyName("tags")] public List<string> Tags { get; init; } = new();

    /// <summary>
    /// 源文件列表。
    /// </summary>
    [JsonPropertyName("sourceFiles")] public List<string> SourceFiles { get; init; } = new();
}

/// <summary>
/// 页面提纲项 DTO。
/// </summary>
public sealed class WikiPageHeadingDto
{
    /// <summary>
    /// 标题层级。
    /// </summary>
    [JsonPropertyName("level")] public int Level { get; init; }

    /// <summary>
    /// 标题文本。
    /// </summary>
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 标题锚点。
    /// </summary>
    [JsonPropertyName("anchor")] public string Anchor { get; init; } = string.Empty;
}

/// <summary>
/// 页面源码覆盖 DTO。
/// </summary>
public sealed class WikiPageSourceCoverageDto
{
    /// <summary>
    /// 主要源文件列表。
    /// </summary>
    [JsonPropertyName("primaryFiles")] public List<string> PrimaryFiles { get; init; } = new();

    /// <summary>
    /// 证据列表。
    /// </summary>
    [JsonPropertyName("evidence")] public List<WikiPageSourceEvidenceDto> Evidence { get; init; } = new();
}

/// <summary>
/// 页面源码证据 DTO。
/// </summary>
public sealed class WikiPageSourceEvidenceDto
{
    /// <summary>
    /// 源文件路径。
    /// </summary>
    [JsonPropertyName("filePath")] public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 证据说明。
    /// </summary>
    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// 关键符号列表。
    /// </summary>
    [JsonPropertyName("symbols")] public List<string> Symbols { get; init; } = new();
}

public sealed class WikiSectionDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("pages")] public List<string> Pages { get; init; } = new();
    [JsonPropertyName("subsections")] public List<string>? Subsections { get; init; }
}

public sealed class WikiStructureDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("pages")] public List<WikiPageDto> Pages { get; init; } = new();
    [JsonPropertyName("sections")] public List<WikiSectionDto> Sections { get; init; } = new();
    [JsonPropertyName("rootSections")] public List<string> RootSections { get; init; } = new();
}

public sealed class WikiTaskResponseDto
{
    [JsonPropertyName("from_cache")] public bool FromCache { get; init; }
    [JsonPropertyName("repo")] public RepoInfoDto Repo { get; init; } = new();
    [JsonPropertyName("language")] public string Language { get; init; } = "zh";
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("wiki_structure")] public WikiStructureDto WikiStructure { get; init; } = new();
    [JsonPropertyName("generated_pages")] public Dictionary<string, WikiPageDto> GeneratedPages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RepoInfoDto
{
    [JsonPropertyName("owner")] public string Owner { get; init; } = string.Empty;
    [JsonPropertyName("repo")] public string Repo { get; init; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; init; } = "github";
    [JsonPropertyName("repoUrl")] public string? RepoUrl { get; init; }
    [JsonPropertyName("localPath")] public string? LocalPath { get; init; }
}

public sealed class WikiExportRequestDto
{
    [JsonPropertyName("repo_url")] public string RepoUrl { get; init; } = string.Empty;
    [JsonPropertyName("format")] public string Format { get; init; } = "markdown";
    [JsonPropertyName("pages")] public List<WikiPageDto> Pages { get; init; } = new();
}
