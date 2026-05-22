using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_templates")]
public class PromptTemplate
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(Length = 128)]
    public string Slug { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 16)]
    public string Layer { get; set; } = "system";

    [SugarColumn(Length = 16)]
    public string ScopeType { get; set; } = "global";

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ScopeValue { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string Category { get; set; } = "general";

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SubCategory { get; set; }

    public int Priority { get; set; }

    [SugarColumn(ColumnDataType = "text[]", IsNullable = true)]
    public string[]? ApplicableProviders { get; set; }

    [SugarColumn(ColumnDataType = "text[]", IsNullable = true)]
    public string[]? Variables { get; set; }

    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(RepositoryPromptOverride.PromptTemplateId))]
    public List<RepositoryPromptOverride> RepositoryOverrides { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(PromptTemplateHistory.PromptTemplateId))]
    public List<PromptTemplateHistory> Versions { get; set; } = new();
}
