namespace Heimdall.Core.Entities;

public class WikiPage
{
    /// <summary>页面主键</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>所属 Wiki 版本（V4 必填锚点）</summary>
    public Guid WikiVersionId { get; set; }
    /// <summary>所属 Wiki 版本导航属性</summary>
    public WikiVersion WikiVersion { get; set; } = null!;
    /// <summary>关联的任务 ID（可选）</summary>
    public Guid? TaskId { get; set; }
    /// <summary>关联的任务记录</summary>
    public TaskRecord? Task { get; set; }
    /// <summary>页面顺序</summary>
    public int PageOrder { get; set; }
    /// <summary>页面标题</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>导航标题（可短于 Title）</summary>
    public string? NavTitle { get; set; }
    /// <summary>页面正文 Markdown</summary>
    public string? ContentMarkdown { get; set; }
    /// <summary>父页面 ID（层级关系）</summary>
    public Guid? ParentPageId { get; set; }
    /// <summary>父页面导航属性</summary>
    public WikiPage? ParentPage { get; set; }
    /// <summary>页面类型：section / article / overview / appendix</summary>
    public string PageType { get; set; } = "article";
    /// <summary>重要程度：high / medium / low</summary>
    public string Importance { get; set; } = "medium";
    /// <summary>层级深度</summary>
    public int Depth { get; set; }
    /// <summary>页面结构化目录 JSON</summary>
    public string? OutlineJson { get; set; }
    /// <summary>页面摘要文本</summary>
    public string? Summary { get; set; }
    /// <summary>来源文件、符号、版本覆盖信息 JSON</summary>
    public string? SourceCoverageJson { get; set; }
    /// <summary>关联的源文件路径列表</summary>
    public string[]? FilePaths { get; set; }
    /// <summary>Token 数估算</summary>
    public int? TokenCount { get; set; }
    /// <summary>页面状态：ready / stale / generating</summary>
    public string Status { get; set; } = "ready";
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>子页面集合</summary>
    public ICollection<WikiPage> Children { get; set; } = new List<WikiPage>();
    /// <summary>作为源头的页面关系</summary>
    public ICollection<WikiPageRelation> SourceRelations { get; set; } = new List<WikiPageRelation>();
    /// <summary>作为目标的页面关系</summary>
    public ICollection<WikiPageRelation> TargetRelations { get; set; } = new List<WikiPageRelation>();
}
