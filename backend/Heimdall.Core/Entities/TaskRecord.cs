namespace Heimdall.Core.Entities;

/// <summary>
/// 统一后台任务记录。
/// 该实体承载任务基础状态、版本回写信息、阶段推进信息与恢复锚点，
/// 用于确保“任务完成”与“结果已稳定落库”保持一致。
/// </summary>
public class TaskRecord
{
    /// <summary>
    /// 任务主标识。
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// 任务类型。
    /// 当前阶段主要为 wiki。
    /// </summary>
    public string TaskType { get; set; } = "wiki";

    /// <summary>
    /// 任务整体状态。
    /// 常见值包括 pending、running、completed、failed、cancelled。
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 关联仓库标识。
    /// </summary>
    public Guid? RepositoryId { get; set; }

    /// <summary>
    /// 关联仓库实体。
    /// </summary>
    public Repository? Repository { get; set; }

    /// <summary>
    /// 源分支名称。
    /// </summary>
    public string SourceBranch { get; set; } = "main";

    /// <summary>
    /// 发起用户标识。
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 发起用户实体。
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 请求去重哈希。
    /// </summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>
    /// 本次任务使用的模型提供方。
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 本次任务使用的模型名称。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 输出语言。
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 当前进度百分比。
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// 当前进度说明。
    /// </summary>
    public string? ProgressMessage { get; set; }

    /// <summary>
    /// Prompt Token 总量。
    /// </summary>
    public int TotalPromptTokens { get; set; }

    /// <summary>
    /// Completion Token 总量。
    /// </summary>
    public int TotalCompletionTokens { get; set; }

    /// <summary>
    /// 任务结果摘要 JSON。
    /// 该字段只保留摘要信息，复杂阶段产物应写入 task_artifacts。
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 当前执行阶段名称。
    /// 例如 queued、repository_preparation、structure_planning、page_generation、persistence、code_embedding、wiki_embedding。
    /// </summary>
    public string CurrentStage { get; set; } = "queued";

    /// <summary>
    /// 当前阶段状态。
    /// 常见值包括 pending、running、completed、failed。
    /// </summary>
    public string CurrentStageStatus { get; set; } = "pending";

    /// <summary>
    /// 最近一次成功完成的阶段名称。
    /// 用于失败恢复时快速定位恢复点。
    /// </summary>
    public string? LastSuccessfulStage { get; set; }

    /// <summary>
    /// 最近一次成功写入的工件标识。
    /// 可用于直接定位恢复点与审计工件。
    /// </summary>
    public Guid? LastArtifactId { get; set; }

    /// <summary>
    /// 执行尝试次数。
    /// 每次真正进入执行主链路时递增。
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>V2: 目标分支</summary>
    public string? TargetBranch { get; set; }
    /// <summary>V2: 实际生成使用的仓库快照版本</summary>
    public Guid? ResolvedRepositoryVersionId { get; set; }
    public RepositoryVersion? ResolvedRepositoryVersion { get; set; }
    /// <summary>V2: 生成的 Wiki 版本</summary>
    public Guid? ResultWikiVersionId { get; set; }
    public WikiVersion? ResultWikiVersion { get; set; }
    /// <summary>V2: 刷新策略 current / latest</summary>
    public string? RefreshStrategy { get; set; }
    /// <summary>V2: 是否强制刷新</summary>
    public bool ForceRefresh { get; set; }
    /// <summary>V2: 生成配置摘要</summary>
    public string? ConfigHash { get; set; }

    /// <summary>
    /// 记录创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最近更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 实际开始执行时间。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 执行结束时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 任务过程中的 LLM 调用日志集合。
    /// </summary>
    public ICollection<TaskLlmCallLog> LlmCallLogs { get; set; } = new List<TaskLlmCallLog>();

    /// <summary>
    /// 任务生成的页面集合。
    /// </summary>
    public ICollection<WikiPage> WikiPages { get; set; } = new List<WikiPage>();

    /// <summary>
    /// 任务工件集合。
    /// </summary>
    public ICollection<TaskArtifact> Artifacts { get; set; } = new List<TaskArtifact>();
}
