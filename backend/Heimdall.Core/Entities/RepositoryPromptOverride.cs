namespace Heimdall.Core.Entities;

public class RepositoryPromptOverride
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    public Guid PromptTemplateId { get; set; }
    public PromptTemplate PromptTemplate { get; set; } = null!;
    public string? OverrideContent { get; set; }
    public bool IsEnabled { get; set; } = true;
}
