namespace Heimdall.Core.Models;

/// <summary>
/// 任务入队负载。
/// 该对象既承载任务去重所需的基础信息，也承载 Worker 执行 Wiki 任务时所需的上下文参数。
/// </summary>
public class TaskEnqueueRequest
{
    /// <summary>
    /// 已存在任务记录的标识。
    /// 当调用方先落库、后入队时，Worker 依赖该字段重新读取任务状态并执行。
    /// </summary>
    public Guid? TaskId { get; set; }

    public Guid? RepositoryId { get; set; }
    public string TaskType { get; set; } = "wiki";
    public string SourceBranch { get; set; } = "main";
    public Guid? UserId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Language { get; set; }
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>
    /// 仓库可访问地址。
    /// Wiki Worker 会使用该地址克隆或更新仓库内容。
    /// </summary>
    public string? RepoUrl { get; set; }

    /// <summary>
    /// 仓库来源类型，例如 github、gitlab、bitbucket 或 local。
    /// </summary>
    public string? RepoType { get; set; }

    /// <summary>
    /// 访问私有仓库时使用的临时令牌。
    /// </summary>
    public string? Token { get; set; }

    public string? CustomModel { get; set; }
    public bool ForceRefresh { get; set; }
    public bool Comprehensive { get; set; } = true;

    /// <summary>
    /// 生成档位。
    /// 该字段与 WikiVersion.GenerationProfile 保持一致，用于区分 concise / comprehensive 等生成模式。
    /// </summary>
    public string GenerationProfile { get; set; } = "comprehensive";

    public string? Question { get; set; }
    public List<ChatMessage>? History { get; set; }
    public bool DeepResearch { get; set; }
    public string? FilePath { get; set; }
}
