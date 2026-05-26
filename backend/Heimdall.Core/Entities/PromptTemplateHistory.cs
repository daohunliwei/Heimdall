using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_template_history")]
public class PromptTemplateHistory
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "PromptTemplateId")]
    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    [SugarColumn(ColumnName = "Version")]
    public int Version { get; set; }

    [SugarColumn(ColumnName = "TemplateContent", ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "ChangedBy", IsNullable = true)]
    public Guid? ChangedBy { get; set; }

    [SugarColumn(ColumnName = "ChangedAt")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
