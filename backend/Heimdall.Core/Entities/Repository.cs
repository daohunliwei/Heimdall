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

    [SugarColumn(ColumnName = "Owner", Length = 128)]
    public string Owner { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "RepoName", Length = 128)]
    public string RepoName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "RepoType", Length = 16)]
    public string RepoType { get; set; } = "github";

    [SugarColumn(ColumnName = "RepoUrl", ColumnDataType = "text", IsNullable = true)]
    public string? RepoUrl { get; set; }

    [SugarColumn(ColumnName = "CloneUrl", ColumnDataType = "text", IsNullable = true)]
    public string? CloneUrl { get; set; }

    [SugarColumn(ColumnName = "DefaultBranch", Length = 128)]
    public string DefaultBranch { get; set; } = "main";

    [SugarColumn(ColumnName = "DefaultLanguage", Length = 8)]
    public string DefaultLanguage { get; set; } = "zh";

    [SugarColumn(ColumnName = "Description", ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "is_archived")]
    public bool IsArchived { get; set; }

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.RepositoryId))]
    public List<TaskRecord> Tasks { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(RepositoryVersion.RepositoryId))]
    public List<RepositoryVersion> RepositoryVersions { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiSpace.RepositoryId))]
    public List<WikiSpace> WikiSpaces { get; set; } = new();
}
