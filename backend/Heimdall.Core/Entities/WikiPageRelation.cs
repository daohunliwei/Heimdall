using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("wiki_page_relations")]
public class WikiPageRelation
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WikiVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(WikiVersionId))]
    public WikiVersion WikiVersion { get; set; } = null!;

    public Guid SourcePageId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(SourcePageId))]
    public WikiPage SourcePage { get; set; } = null!;

    public Guid TargetPageId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(TargetPageId))]
    public WikiPage TargetPage { get; set; } = null!;

    [SugarColumn(ColumnName = "relation_type", Length = 32)]
    public string RelationType { get; set; } = "related_to";

    [SugarColumn(ColumnName = "metadata_json", IsJson = true, IsNullable = true)]
    public string? MetadataJson { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
