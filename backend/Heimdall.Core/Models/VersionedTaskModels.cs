using Heimdall.Core.Entities;

namespace Heimdall.Core.Models;

/// <summary>
/// 版本化派生任务的通用执行选项。
/// 该对象显式携带 RepositoryVersion 与 WikiVersion 继承参数，
/// 用于确保 Ask、Slides、Workshop 三类能力始终绑定到同一知识底座。
/// </summary>
public sealed class VersionedTaskExecutionOptions
{
    /// <summary>
    /// 当前任务所属的仓库标识。
    /// </summary>
    public Guid RepositoryId { get; set; }

    /// <summary>
    /// 调用方显式指定的仓库快照版本标识。
    /// 当该值存在时，后端将优先使用该 RepositoryVersion，并校验与 WikiVersion 的一致性。
    /// </summary>
    public Guid? RepositoryVersionId { get; set; }

    /// <summary>
    /// 调用方显式指定的 Wiki 版本标识。
    /// 当该值存在时，后端将优先使用该 WikiVersion 作为页面与工件来源。
    /// </summary>
    public Guid? WikiVersionId { get; set; }

    /// <summary>
    /// 期望输出语言。
    /// 若未指定，则优先使用 Wiki 空间语言，其次回退到仓库默认语言。
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// 期望绑定的分支名称。
    /// 当未显式传入 RepositoryVersionId 时，该值用于回退解析最新仓库快照。
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// 调用方指定的聊天模型提供方。
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 调用方指定的标准模型名称。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 调用方指定的自定义模型名称。
    /// 若存在，则优先作为真实执行模型。
    /// </summary>
    public string? CustomModel { get; set; }
}

/// <summary>
/// 对话历史消息。
/// 该模型用于从控制器向业务服务传递 Ask 场景的上下文对话。
/// </summary>
public sealed class TaskConversationMessage
{
    /// <summary>
    /// 消息角色。
    /// 常见值包括 user、assistant、system。
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    /// 消息正文。
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Ask 派生任务的执行请求。
/// </summary>
public sealed class AskTaskExecutionRequest
{
    /// <summary>
    /// 版本继承与模型执行选项。
    /// </summary>
    public VersionedTaskExecutionOptions Options { get; set; } = new();

    /// <summary>
    /// 当前用户问题。
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// 历史对话消息集合。
    /// </summary>
    public IReadOnlyList<TaskConversationMessage> History { get; set; } = [];

    /// <summary>
    /// 是否启用深度研究模式。
    /// 深度模式下会扩大检索窗口并要求回答覆盖更多证据。
    /// </summary>
    public bool DeepResearch { get; set; }

    /// <summary>
    /// 可选文件路径提示。
    /// 当用户只关注某个文件时，该字段可用于在提示词中表达关注范围。
    /// </summary>
    public string? FilePath { get; set; }
}

/// <summary>
/// Slides 派生任务的执行请求。
/// </summary>
public sealed class SlidesTaskExecutionRequest
{
    /// <summary>
    /// 版本继承与模型执行选项。
    /// </summary>
    public VersionedTaskExecutionOptions Options { get; set; } = new();
}

/// <summary>
/// Workshop 派生任务的执行请求。
/// </summary>
public sealed class WorkshopTaskExecutionRequest
{
    /// <summary>
    /// 版本继承与模型执行选项。
    /// </summary>
    public VersionedTaskExecutionOptions Options { get; set; } = new();
}

/// <summary>
/// 从任务工件中提炼出的快照信息。
/// 该对象用于把结构规划、渲染快照、质量报告等中间产物统一暴露给下游派生任务。
/// </summary>
public sealed class KnowledgeArtifactSnapshot
{
    /// <summary>
    /// 工件类型。
    /// 例如 planning_artifact、render_artifact、quality_report_artifact。
    /// </summary>
    public string ArtifactType { get; set; } = string.Empty;

    /// <summary>
    /// 工件键。
    /// 用于区分同类型下的不同逻辑产物。
    /// </summary>
    public string ArtifactKey { get; set; } = string.Empty;

    /// <summary>
    /// 工件所属阶段。
    /// </summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    /// 工件摘要说明。
    /// 该字段优先用于派生任务快速理解工件含义。
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 工件原始 JSON 载荷。
    /// 当派生任务需要更高保真度的上下文时，可继续解析该内容。
    /// </summary>
    public string PayloadJson { get; set; } = "{}";
}

/// <summary>
/// Ask、Slides、Workshop 共享的版本化知识上下文。
/// 该对象显式暴露仓库、版本、页面与工件，避免派生任务回退到旧 Wiki 聚合数据。
/// </summary>
public sealed class VersionedKnowledgeContext
{
    /// <summary>
    /// 目标仓库实体。
    /// </summary>
    public Repository Repository { get; set; } = null!;

    /// <summary>
    /// 已解析的仓库快照版本。
    /// </summary>
    public RepositoryVersion RepositoryVersion { get; set; } = null!;

    /// <summary>
    /// 已解析的 Wiki 版本。
    /// </summary>
    public WikiVersion WikiVersion { get; set; } = null!;

    /// <summary>
    /// 与 WikiVersion 绑定的页面集合。
    /// 页面顺序与展示顺序一致。
    /// </summary>
    public IReadOnlyList<WikiPage> Pages { get; set; } = [];

    /// <summary>
    /// 与当前 WikiVersion 同源的任务工件快照集合。
    /// </summary>
    public IReadOnlyList<KnowledgeArtifactSnapshot> Artifacts { get; set; } = [];

    /// <summary>
    /// 最终生效的语言。
    /// </summary>
    public string EffectiveLanguage { get; set; } = "zh";

    /// <summary>
    /// 最终生效的分支。
    /// </summary>
    public string EffectiveBranch { get; set; } = "main";
}

/// <summary>
/// Ask 阶段说明。
/// 该模型用于向前端回传版本绑定、检索命中与回答生成等关键步骤摘要。
/// </summary>
public sealed class AskExecutionStage
{
    /// <summary>
    /// 阶段标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 阶段内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 所属轮次。
    /// 当前实现固定为 1，用于兼容前端既有结构。
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// 阶段类型。
    /// 常见值包括 plan、update、conclusion。
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Ask 任务执行结果。
/// </summary>
public sealed class AskTaskExecutionResult
{
    /// <summary>
    /// 回答正文。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Ask 阶段摘要集合。
    /// </summary>
    public IReadOnlyList<AskExecutionStage> Stages { get; set; } = [];

    /// <summary>
    /// 是否执行完成。
    /// </summary>
    public bool Complete { get; set; }

    /// <summary>
    /// 实际执行轮次。
    /// </summary>
    public int Iterations { get; set; } = 1;

    /// <summary>
    /// 生效的仓库版本标识。
    /// </summary>
    public Guid RepositoryVersionId { get; set; }

    /// <summary>
    /// 生效的 Wiki 版本标识。
    /// </summary>
    public Guid WikiVersionId { get; set; }
}

/// <summary>
/// 单页幻灯片结果。
/// </summary>
public sealed class GeneratedSlideResult
{
    /// <summary>
    /// 幻灯片标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片摘要文本。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片 HTML 正文。
    /// </summary>
    public string Html { get; set; } = string.Empty;
}

/// <summary>
/// Slides 任务执行结果。
/// </summary>
public sealed class SlidesTaskExecutionResult
{
    /// <summary>
    /// 幻灯片规划文本。
    /// </summary>
    public string Plan { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片集合。
    /// </summary>
    public IReadOnlyList<GeneratedSlideResult> Slides { get; set; } = [];

    /// <summary>
    /// 生效的仓库版本标识。
    /// </summary>
    public Guid RepositoryVersionId { get; set; }

    /// <summary>
    /// 生效的 Wiki 版本标识。
    /// </summary>
    public Guid WikiVersionId { get; set; }
}

/// <summary>
/// Workshop 任务执行结果。
/// </summary>
public sealed class WorkshopTaskExecutionResult
{
    /// <summary>
    /// 训练营 Markdown 内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 生效的仓库版本标识。
    /// </summary>
    public Guid RepositoryVersionId { get; set; }

    /// <summary>
    /// 生效的 Wiki 版本标识。
    /// </summary>
    public Guid WikiVersionId { get; set; }
}
