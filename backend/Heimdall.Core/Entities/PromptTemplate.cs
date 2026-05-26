using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_templates")]
public class PromptTemplate
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "Slug", Length = 128)]
    public string Slug { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Name", Length = 128)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Layer", Length = 16)]
    public string Layer { get; set; } = "system";

    [SugarColumn(ColumnName = "ScopeType", Length = 16)]
    public string ScopeType { get; set; } = "global";

    [SugarColumn(ColumnName = "ScopeValue", Length = 128, IsNullable = true)]
    public string? ScopeValue { get; set; }

    [SugarColumn(ColumnName = "TemplateContent", ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Category", Length = 64)]
    public string Category { get; set; } = "general";

    [SugarColumn(ColumnName = "SubCategory", Length = 64, IsNullable = true)]
    public string? SubCategory { get; set; }

    [SugarColumn(ColumnName = "Priority")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "ApplicableProviders", ColumnDataType = "text[]", IsArray = true, IsNullable = true)]
    public string[]? ApplicableProviders { get; set; }

    [SugarColumn(ColumnName = "Variables", ColumnDataType = "text[]", IsArray = true, IsNullable = true)]
    public string[]? Variables { get; set; }

    [SugarColumn(ColumnName = "IsSystem")]
    public bool IsSystem { get; set; }

    [SugarColumn(ColumnName = "IsActive")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "Version")]
    public int Version { get; set; } = 1;

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(RepositoryPromptOverride.PromptTemplateId))]
    public List<RepositoryPromptOverride> RepositoryOverrides { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(PromptTemplateHistory.PromptTemplateId))]
    public List<PromptTemplateHistory> Versions { get; set; } = new();
}
