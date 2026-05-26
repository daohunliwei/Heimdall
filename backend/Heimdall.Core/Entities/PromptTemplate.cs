using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_templates")]
public class PromptTemplate
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "slug", Length = 128)]
    public string Slug { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "name", Length = 128)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "layer", Length = 16)]
    public string Layer { get; set; } = "system";

    [SugarColumn(ColumnName = "scope_type", Length = 16)]
    public string ScopeType { get; set; } = "global";

    [SugarColumn(ColumnName = "scope_value", Length = 128, IsNullable = true)]
    public string? ScopeValue { get; set; }

    [SugarColumn(ColumnName = "template_content", ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "category", Length = 64)]
    public string Category { get; set; } = "general";

    [SugarColumn(ColumnName = "sub_category", Length = 64, IsNullable = true)]
    public string? SubCategory { get; set; }

    [SugarColumn(ColumnName = "priority")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "applicable_providers", ColumnDataType = "text[]", IsArray = true, IsNullable = true)]
    public string[]? ApplicableProviders { get; set; }

    [SugarColumn(ColumnName = "variables", ColumnDataType = "text[]", IsArray = true, IsNullable = true)]
    public string[]? Variables { get; set; }

    [SugarColumn(ColumnName = "is_system")]
    public bool IsSystem { get; set; }

    [SugarColumn(ColumnName = "is_active")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "version")]
    public int Version { get; set; } = 1;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(RepositoryPromptOverride.PromptTemplateId))]
    public List<RepositoryPromptOverride> RepositoryOverrides { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(PromptTemplateHistory.PromptTemplateId))]
    public List<PromptTemplateHistory> Versions { get; set; } = new();
}
