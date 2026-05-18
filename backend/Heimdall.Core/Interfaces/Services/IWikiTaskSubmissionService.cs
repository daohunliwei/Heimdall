using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// Wiki 任务统一提交服务接口。
/// 该接口用于承载 `/wiki/refresh` 正式入口对应的任务创建、去重和队列调度逻辑。
/// </summary>
public interface IWikiTaskSubmissionService
{
    /// <summary>
    /// 按刷新语义提交一次 Wiki 任务。
    /// 该入口会先执行版本决策，再决定复用已有结果还是排队新的后台任务。
    /// </summary>
    Task<WikiTaskSubmissionResult> SubmitRefreshAsync(WikiTaskSubmissionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Wiki 任务统一提交请求。
/// 该对象封装了任务创建、版本发现与队列调度所需的全部入参。
/// </summary>
public sealed class WikiTaskSubmissionRequest
{
    /// <summary>
    /// 仓库主标识。
    /// </summary>
    public Guid RepositoryId { get; set; }

    /// <summary>
    /// 目标分支。
    /// 若为空则使用仓库默认分支。
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// 刷新策略。
    /// 仅在刷新入口中参与版本决策，支持 current / latest。
    /// </summary>
    public string? RefreshStrategy { get; set; }

    /// <summary>
    /// 是否强制刷新。
    /// 为 true 时即便没有新提交，也允许重新排队生成。
    /// </summary>
    public bool ForceRefresh { get; set; }

    /// <summary>
    /// 模型提供方。
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 模型名称。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 自定义模型名称。
    /// </summary>
    public string? CustomModel { get; set; }

    /// <summary>
    /// 输出语言。
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 是否使用综合视图生成。
    /// </summary>
    public bool Comprehensive { get; set; } = true;

    /// <summary>
    /// 生成档位。
    /// </summary>
    public string? GenerationProfile { get; set; }

    /// <summary>
    /// 访问私有仓库时的令牌。
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 发起用户标识。
    /// 当前阶段可为空，保留给未来审计链路使用。
    /// </summary>
    public Guid? UserId { get; set; }
}

/// <summary>
/// Wiki 任务统一提交结果。
/// 返回字段仅服务于 V3 的正式任务与版本语义。
/// </summary>
public sealed class WikiTaskSubmissionResult
{
    /// <summary>
    /// 任务标识。
    /// 如果本次复用已完成结果且没有新建任务，则该字段仍可指向被复用的任务。
    /// </summary>
    public Guid? TaskId { get; set; }

    /// <summary>
    /// 任务当前状态。
    /// 常见值为 pending、running、completed。
    /// </summary>
    public string TaskStatus { get; set; } = "pending";

    /// <summary>
    /// 仓库版本标识。
    /// 对于刷新入口，该字段优先表示本次版本决策得到的 RepositoryVersion。
    /// </summary>
    public Guid? RepositoryVersionId { get; set; }

    /// <summary>
    /// Wiki 版本标识。
    /// 对于复用场景，该字段表示当前可读的有效 WikiVersion。
    /// </summary>
    public Guid? WikiVersionId { get; set; }

    /// <summary>
    /// 结果类型。
    /// 当前阶段主要取值为 queued、reused。
    /// </summary>
    public string ResultType { get; set; } = "queued";

    /// <summary>
    /// 版本变化状态。
    /// 当前阶段主要取值为 changed 或 unchanged。
    /// </summary>
    public string ChangeStatus { get; set; } = "changed";

    /// <summary>
    /// 面向前端的结果说明。
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 预估成本信息（V6 新增）。
    /// </summary>
    public WikiTaskCostEstimate? CostEstimate { get; set; }

    /// <summary>
    /// 小模型质量警告（V6 新增）。
    /// </summary>
    public string? QualityWarning { get; set; }
}

/// <summary>
/// Wiki 任务预估成本（V6 新增）。
/// </summary>
public sealed class WikiTaskCostEstimate
{
    public long EstimatedInputTokens { get; set; }
    public long EstimatedOutputTokens { get; set; }
    public int EstimatedCallCount { get; set; }
    public double EstimatedCostUsd { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
