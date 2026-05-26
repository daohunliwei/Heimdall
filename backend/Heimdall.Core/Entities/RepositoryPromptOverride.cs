using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("repository_prompt_overrides")]
public class RepositoryPromptOverride
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "RepositoryId")]
    public Guid RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository Repository { get; set; } = null!;

    [SugarColumn(ColumnName = "PromptTemplateId")]
    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    [SugarColumn(ColumnName = "OverrideContent", ColumnDataType = "text", IsNullable = true)]
    public string? OverrideContent { get; set; }

    [SugarColumn(ColumnName = "Strategy", Length = 16)]
    public string Strategy { get; set; } = "override";

    [SugarColumn(ColumnName = "Priority")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
