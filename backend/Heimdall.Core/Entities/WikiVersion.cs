namespace Heimdall.Core.Entities;

/// <summary>Wiki 生成版本 — 表示某 Wiki 空间在某个仓库快照上的一次生成结果</summary>
public class WikiVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid WikiSpaceId { get; set; }
    public WikiSpace WikiSpace { get; set; } = null!;
    public Guid RepositoryVersionId { get; set; }
    public RepositoryVersion RepositoryVersion { get; set; } = null!;
    /// <summary>Wiki 版本号，仓库内递增</summary>
    public int VersionNo { get; set; }
    /// <summary>生成模式：current / latest / rebuild</summary>
    public string GenerationMode { get; set; } = "latest";
    /// <summary>生成档位：concise / comprehensive</summary>
    public string GenerationProfile { get; set; } = "comprehensive";
    /// <summary>Prompt 模板版本摘要</summary>
    public string? PromptProfileHash { get; set; }
    /// <summary>Provider + Model 配置摘要</summary>
    public string? ModelProfileHash { get; set; }
    /// <summary>状态：draft / generating / ready / published / failed / superseded</summary>
    public string Status { get; set; } = "draft";
    /// <summary>是否强制刷新生成</summary>
    public bool IsForceRefresh { get; set; }
    /// <summary>页面数量</summary>
    public int? PageCount { get; set; }
    /// <summary>目录深度</summary>
    public int? TocDepth { get; set; }
    /// <summary>版本摘要说明</summary>
    public string? SummaryMarkdown { get; set; }
    /// <summary>结构规划 JSON（阶段 B 工件）</summary>
    public string? StructureJson { get; set; }
    /// <summary>来源任务 ID</summary>
    public Guid? CreatedByTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<WikiPage> WikiPages { get; set; } = new List<WikiPage>();
}
