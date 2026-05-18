namespace Heimdall.Core.Entities;

/// <summary>仓库快照版本 — 以 (repository_id, branch_name, commit_sha) 唯一标识不可变快照</summary>
public class RepositoryVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    /// <summary>分支名</summary>
    public string BranchName { get; set; } = "main";
    /// <summary>提交哈希</summary>
    public string CommitSha { get; set; } = string.Empty;
    /// <summary>文件树指纹，用于快速比较</summary>
    public string? TreeFingerprint { get; set; }
    /// <summary>提交时间</summary>
    public DateTime CommitTime { get; set; }
    /// <summary>提交作者</summary>
    public string? CommitAuthor { get; set; }
    /// <summary>提交说明摘要</summary>
    public string? CommitMessage { get; set; }
    /// <summary>版本状态：active / superseded / deleted</summary>
    public string SourceStatus { get; set; } = "active";
    /// <summary>是否为该分支最新发现的版本</summary>
    public bool IsLatestOnBranch { get; set; }
    /// <summary>版本可信度：exact / inferred / unknown</summary>
    public string VersionSourceConfidence { get; set; } = "exact";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WikiVersion> WikiVersions { get; set; } = new List<WikiVersion>();
}
