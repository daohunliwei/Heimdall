using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("repository_prompt_overrides")]
public class RepositoryPromptOverride
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "repository_id")]
    public Guid RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository Repository { get; set; } = null!;

    [SugarColumn(ColumnName = "prompt_template_id")]
    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    [SugarColumn(ColumnName = "override_content", ColumnDataType = "text", IsNullable = true)]
    public string? OverrideContent { get; set; }

    [SugarColumn(ColumnName = "strategy", Length = 16)]
    public string Strategy { get; set; } = "override";

    [SugarColumn(ColumnName = "priority")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
