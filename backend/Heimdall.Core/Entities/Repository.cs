namespace Heimdall.Core.Entities;

public class Repository
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string RepoType { get; set; } = "github";
    public string? RepoUrl { get; set; }
    public string? CloneUrl { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public string DefaultLanguage { get; set; } = "zh";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();
    public ICollection<Wiki> Wikis { get; set; } = new List<Wiki>();
    public ICollection<EmbeddingDocument> EmbeddingDocuments { get; set; } = new List<EmbeddingDocument>();
}
