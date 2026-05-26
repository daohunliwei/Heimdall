using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("wiki_pages")]
public class WikiPage
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WikiVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(WikiVersionId))]
    public WikiVersion WikiVersion { get; set; } = null!;

    [SugarColumn(IsNullable = true)]
    public Guid? TaskId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TaskId))]
    public TaskRecord? Task { get; set; }

    public int PageOrder { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "nav_title", Length = 256, IsNullable = true)]
    public string? NavTitle { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ContentMarkdown { get; set; }

    [SugarColumn(IsNullable = true)]
    public Guid? ParentPageId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(ParentPageId))]
    public WikiPage? ParentPage { get; set; }

    [SugarColumn(ColumnName = "page_type", Length = 16)]
    public string PageType { get; set; } = "article";

    [SugarColumn(Length = 8)]
    public string Importance { get; set; } = "medium";

    [SugarColumn(ColumnName = "depth")]
    public int Depth { get; set; }

    [SugarColumn(ColumnName = "outline_json", IsJson = true, IsNullable = true)]
    public string? OutlineJson { get; set; }

    [SugarColumn(ColumnName = "summary", ColumnDataType = "text", IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(ColumnName = "source_coverage_json", IsJson = true, IsNullable = true)]
    public string? SourceCoverageJson { get; set; }

    [SugarColumn(ColumnDataType = "text []", IsArray = true, IsNullable = true)]
    public string[]? FilePaths { get; set; }

    [SugarColumn(ColumnName = "token_count", IsNullable = true)]
    public int? TokenCount { get; set; }

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; set; } = "ready";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(WikiPage.ParentPageId))]
    public List<WikiPage> Children { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiPageRelation.SourcePageId))]
    public List<WikiPageRelation> SourceRelations { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiPageRelation.TargetPageId))]
    public List<WikiPageRelation> TargetRelations { get; set; } = new();
}
