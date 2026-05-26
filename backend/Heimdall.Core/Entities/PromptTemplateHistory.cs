using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_template_history")]
public class PromptTemplateHistory
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "prompt_template_id")]
    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    [SugarColumn(ColumnName = "version")]
    public int Version { get; set; }

    [SugarColumn(ColumnName = "template_content", ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "changed_by", IsNullable = true)]
    public Guid? ChangedBy { get; set; }

    [SugarColumn(ColumnName = "changed_at")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
