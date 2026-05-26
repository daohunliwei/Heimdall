using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("repositories")]
public class Repository
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "provider_type", Length = 32)]
    public string ProviderType { get; set; } = "github";

    [SugarColumn(ColumnName = "provider_repository_key", Length = 256, IsNullable = true)]
    public string? ProviderRepositoryKey { get; set; }

    [SugarColumn(ColumnName = "display_name", Length = 512)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "owner", Length = 128)]
    public string Owner { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "repo_name", Length = 128)]
    public string RepoName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "repo_type", Length = 16)]
    public string RepoType { get; set; } = "github";

    [SugarColumn(ColumnName = "repo_url", ColumnDataType = "text", IsNullable = true)]
    public string? RepoUrl { get; set; }

    [SugarColumn(ColumnName = "clone_url", ColumnDataType = "text", IsNullable = true)]
    public string? CloneUrl { get; set; }

    [SugarColumn(ColumnName = "default_branch", Length = 128)]
    public string DefaultBranch { get; set; } = "main";

    [SugarColumn(ColumnName = "default_language", Length = 8)]
    public string DefaultLanguage { get; set; } = "zh";

    [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "is_archived")]
    public bool IsArchived { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.RepositoryId))]
    public List<TaskRecord> Tasks { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(RepositoryVersion.RepositoryId))]
    public List<RepositoryVersion> RepositoryVersions { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiSpace.RepositoryId))]
    public List<WikiSpace> WikiSpaces { get; set; } = new();
}
