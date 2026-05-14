using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

public sealed class WikiPageDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
    [JsonPropertyName("filePaths")] public List<string> FilePaths { get; init; } = new();
    [JsonPropertyName("importance")] public string Importance { get; init; } = "medium";
    [JsonPropertyName("relatedPages")] public List<string> RelatedPages { get; init; } = new();
    [JsonPropertyName("parentId")] public string? ParentId { get; init; }
    [JsonPropertyName("isSection")] public bool? IsSection { get; init; }
    [JsonPropertyName("children")] public List<string>? Children { get; init; }
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
