namespace Heimdall.Core.Entities;

public class WikiPage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid WikiId { get; set; }
    public Wiki Wiki { get; set; } = null!;
    /// <summary>V2: 所属 Wiki 版本（新增，过渡期可为空）</summary>
    public Guid? WikiVersionId { get; set; }
    public WikiVersion? WikiVersion { get; set; }
    public Guid? TaskId { get; set; }
    public TaskRecord? Task { get; set; }
    public int PageOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>导航标题（可短于 Title）</summary>
    public string? NavTitle { get; set; }
    /// <summary>页面内容</summary>
    public string? ContentMarkdown { get; set; }
    public Guid? ParentPageId { get; set; }
    public WikiPage? ParentPage { get; set; }
    /// <summary>页面类型：section / article / overview / appendix</summary>
    public string PageType { get; set; } = "article";
    public string Importance { get; set; } = "medium";
    /// <summary>层级深度</summary>
    public int Depth { get; set; }
    /// <summary>页面结构化目录 JSON</summary>
    public string? OutlineJson { get; set; }
    /// <summary>页面摘要</summary>
    public string? Summary { get; set; }
    /// <summary>来源文件、符号、版本覆盖信息 JSON</summary>
    public string? SourceCoverageJson { get; set; }
    public string[]? FilePaths { get; set; }
    /// <summary>Token 数估算</summary>
    public int? TokenCount { get; set; }
    /// <summary>页面状态：ready / stale / generating</summary>
    public string Status { get; set; } = "ready";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WikiPage> Children { get; set; } = new List<WikiPage>();
    public ICollection<WikiPageRelation> SourceRelations { get; set; } = new List<WikiPageRelation>();
    public ICollection<WikiPageRelation> TargetRelations { get; set; } = new List<WikiPageRelation>();
}
