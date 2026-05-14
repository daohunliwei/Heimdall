namespace Heimdall.Core.Entities;

public class Repository
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>仓库平台类型：github / gitlab / bitbucket / local</summary>
    public string ProviderType { get; set; } = "github";
    /// <summary>上游平台可稳定识别的仓库键，优先使用平台原生 ID</summary>
    public string? ProviderRepositoryKey { get; set; }
    /// <summary>展示名称，形如 owner/repo</summary>
    public string DisplayName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string RepoType { get; set; } = "github";
    public string? RepoUrl { get; set; }
    public string? CloneUrl { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public string DefaultLanguage { get; set; } = "zh";
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();
    public ICollection<Wiki> Wikis { get; set; } = new List<Wiki>();
    public ICollection<EmbeddingDocument> EmbeddingDocuments { get; set; } = new List<EmbeddingDocument>();
    public ICollection<RepositoryVersion> RepositoryVersions { get; set; } = new List<RepositoryVersion>();
    public ICollection<WikiSpace> WikiSpaces { get; set; } = new List<WikiSpace>();
}
