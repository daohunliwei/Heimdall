using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("wiki_spaces")]
public class WikiSpace
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "RepositoryId")]
    public Guid RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository Repository { get; set; } = null!;

    [SugarColumn(ColumnName = "language", Length = 8)]
    public string Language { get; set; } = "zh";

    [SugarColumn(ColumnName = "view_type", Length = 32)]
    public string ViewType { get; set; } = "default";

    [SugarColumn(ColumnName = "title")]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "published_wiki_version_id", IsNullable = true)]
    public Guid? PublishedWikiVersionId { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(WikiVersion.WikiSpaceId))]
    public List<WikiVersion> WikiVersions { get; set; } = new();
}
