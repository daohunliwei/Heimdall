namespace Heimdall.Core.Entities;

/// <summary>Wiki 页面关系 — 显式建模页面间关系，支持 parent、depends_on、related_to 等类型</summary>
public class WikiPageRelation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid WikiVersionId { get; set; }
    public WikiVersion WikiVersion { get; set; } = null!;
    public Guid SourcePageId { get; set; }
    public WikiPage SourcePage { get; set; } = null!;
    public Guid TargetPageId { get; set; }
    public WikiPage TargetPage { get; set; } = null!;
    /// <summary>关系类型：parent / depends_on / related_to / see_also / generated_from / diff_against</summary>
    public string RelationType { get; set; } = "related_to";
    /// <summary>关系元数据 JSON</summary>
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
