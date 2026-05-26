using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("repository_versions")]
public class RepositoryVersion
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "RepositoryId")]
    public Guid RepositoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryId))]
    public Repository Repository { get; set; } = null!;

    [SugarColumn(ColumnName = "branch_name", Length = 256)]
    public string BranchName { get; set; } = "main";

    [SugarColumn(ColumnName = "commit_sha", Length = 64)]
    public string CommitSha { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "tree_fingerprint", Length = 128, IsNullable = true)]
    public string? TreeFingerprint { get; set; }

    [SugarColumn(ColumnName = "commit_time")]
    public DateTime CommitTime { get; set; }

    [SugarColumn(ColumnName = "commit_author", Length = 256, IsNullable = true)]
    public string? CommitAuthor { get; set; }

    [SugarColumn(ColumnName = "commit_message", ColumnDataType = "text", IsNullable = true)]
    public string? CommitMessage { get; set; }

    [SugarColumn(ColumnName = "source_status", Length = 32)]
    public string SourceStatus { get; set; } = "active";

    [SugarColumn(ColumnName = "is_latest_on_branch")]
    public bool IsLatestOnBranch { get; set; }

    [SugarColumn(ColumnName = "version_source_confidence", Length = 16)]
    public string VersionSourceConfidence { get; set; } = "exact";

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(WikiVersion.RepositoryVersionId))]
    public List<WikiVersion> WikiVersions { get; set; } = new();
}
