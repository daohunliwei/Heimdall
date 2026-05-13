namespace Heimdall.Core.Models;

/// <summary>
/// Wiki 页面 DTO——业务层使用的 Wiki 页面定义。
/// </summary>
public class WikiPageDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public string Importance { get; set; } = "medium";
    public List<string> RelatedPages { get; set; } = new();
    public string? ParentId { get; set; }
    public bool? IsSection { get; set; }
    public List<string>? Children { get; set; }
}

public class WikiSectionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Pages { get; set; } = new();
    public List<string>? Subsections { get; set; }
}

public class WikiStructureDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WikiPageDto> Pages { get; set; } = new();
    public List<WikiSectionDto> Sections { get; set; } = new();
    public List<string> RootSections { get; set; } = new();
}

public class WikiGenerationResult
{
    public bool FromCache { get; set; }
    public string? RepoOwner { get; set; }
    public string? RepoName { get; set; }
    public string? RepoType { get; set; }
    public string Language { get; set; } = "zh";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public WikiStructureDto WikiStructure { get; set; } = new();
    public Dictionary<string, WikiPageDto> GeneratedPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = new();
}
