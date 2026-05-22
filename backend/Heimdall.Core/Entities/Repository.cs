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

    [SugarColumn(Length = 128)]
    public string Owner { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string RepoName { get; set; } = string.Empty;

    [SugarColumn(Length = 16)]
    public string RepoType { get; set; } = "github";

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? RepoUrl { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? CloneUrl { get; set; }

    [SugarColumn(Length = 128)]
    public string DefaultBranch { get; set; } = "main";

    [SugarColumn(Length = 8)]
    public string DefaultLanguage { get; set; } = "zh";

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "is_archived")]
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.RepositoryId))]
    public List<TaskRecord> Tasks { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(RepositoryVersion.RepositoryId))]
    public List<RepositoryVersion> RepositoryVersions { get; set; } = new();

    [Navigate(NavigateType.OneToMany, nameof(WikiSpace.RepositoryId))]
    public List<WikiSpace> WikiSpaces { get; set; } = new();
}
