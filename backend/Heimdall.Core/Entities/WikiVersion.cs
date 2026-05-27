using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("wiki_versions")]
public class WikiVersion
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "wiki_space_id")]
    public Guid WikiSpaceId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(WikiSpaceId))]
    public WikiSpace WikiSpace { get; set; } = null!;

    [SugarColumn(ColumnName = "repository_version_id")]
    public Guid RepositoryVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryVersionId))]
    public RepositoryVersion RepositoryVersion { get; set; } = null!;

    [SugarColumn(ColumnName = "ast_version_id", IsNullable = true)]
    public Guid? AstVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(AstVersionId))]
    public AstVersion? AstVersion { get; set; }

    [SugarColumn(ColumnName = "version_no")]
    public int VersionNo { get; set; }

    [SugarColumn(ColumnName = "generation_mode", Length = 16)]
    public string GenerationMode { get; set; } = "latest";

    [SugarColumn(ColumnName = "generation_profile", Length = 32)]
    public string GenerationProfile { get; set; } = "comprehensive";

    [SugarColumn(ColumnName = "prompt_profile_hash", Length = 64, IsNullable = true)]
    public string? PromptProfileHash { get; set; }

    [SugarColumn(ColumnName = "model_profile_hash", Length = 64, IsNullable = true)]
    public string? ModelProfileHash { get; set; }

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; set; } = "draft";

    [SugarColumn(ColumnName = "is_force_refresh")]
    public bool IsForceRefresh { get; set; }

    [SugarColumn(ColumnName = "page_count", IsNullable = true)]
    public int? PageCount { get; set; }

    [SugarColumn(ColumnName = "toc_depth", IsNullable = true)]
    public int? TocDepth { get; set; }

    [SugarColumn(ColumnName = "summary_markdown", ColumnDataType = "text", IsNullable = true)]
    public string? SummaryMarkdown { get; set; }

    [SugarColumn(ColumnName = "structure_json", ColumnDataType = "text", IsNullable = true)]
    public string? StructureJson { get; set; }

    [SugarColumn(ColumnName = "created_by_task_id", IsNullable = true)]
    public Guid? CreatedByTaskId { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "completed_at", IsNullable = true)]
    public DateTime? CompletedAt { get; set; }

    [Navigate(NavigateType.OneToMany, nameof(WikiPage.WikiVersionId))]
    public List<WikiPage> WikiPages { get; set; } = new();
}
