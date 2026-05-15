namespace Heimdall.Core.Entities;

public class PromptTemplateHistory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PromptTemplateId { get; set; }
    public PromptTemplate PromptTemplate { get; set; } = null!;
    public int Version { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
