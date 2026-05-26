using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("prompt_template_history")]
public class PromptTemplateHistory
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PromptTemplateId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(PromptTemplateId))]
    public PromptTemplate PromptTemplate { get; set; } = null!;

    public int Version { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string TemplateContent { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
