namespace Heimdall.Core.Entities;

/// <summary>Wiki 逻辑空间 — 表示某仓库在某语言、某视角下的逻辑 Wiki</summary>
public class WikiSpace
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    /// <summary>语言</summary>
    public string Language { get; set; } = "zh";
    /// <summary>视角类型：default / architecture / onboarding / security</summary>
    public string ViewType { get; set; } = "default";
    /// <summary>逻辑 Wiki 标题</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>逻辑 Wiki 描述</summary>
    public string? Description { get; set; }
    /// <summary>当前发布中的版本 ID</summary>
    public Guid? PublishedWikiVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WikiVersion> WikiVersions { get; set; } = new List<WikiVersion>();
}
