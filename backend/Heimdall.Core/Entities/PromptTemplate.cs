namespace Heimdall.Core.Entities;

public class PromptTemplate
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Layer { get; set; } = "system";
    public string ScopeType { get; set; } = "global";
    public string? ScopeValue { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public string[]? Variables { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RepositoryPromptOverride> RepositoryOverrides { get; set; } = new List<RepositoryPromptOverride>();
    public ICollection<PromptTemplateHistory> Versions { get; set; } = new List<PromptTemplateHistory>();
}
