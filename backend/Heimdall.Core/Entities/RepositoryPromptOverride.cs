using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("repository_prompt_overrides")]
public class RepositoryPromptOverride
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository Repository { get; set; } = null!;

    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? OverrideContent { get; set; }

    [SugarColumn(Length = 16)]
    public string Strategy { get; set; } = "override";

    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
