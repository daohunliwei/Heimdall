using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 后端任务请求基类。
/// </summary>
public abstract class TaskRequestBase
{
    /// <summary>
    /// 仓库地址或本地目录。
    /// </summary>
    [JsonPropertyName("repo_url")]
    public string? RepoUrl { get; init; }

    /// <summary>
    /// 仓库所有者。
    /// </summary>
    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    /// <summary>
    /// 仓库名。
    /// </summary>
    [JsonPropertyName("repo")]
    public string? Repo { get; init; }

    /// <summary>
    /// 仓库类型。
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// 私有仓库访问令牌。
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>
    /// 模型提供方。
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// 模型名称。
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// 自定义模型名称。
    /// </summary>
    [JsonPropertyName("custom_model")]
    public string? CustomModel { get; init; }

    /// <summary>
    /// 输出语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// 排除目录列表，换行分隔。
    /// </summary>
    [JsonPropertyName("excluded_dirs")]
    public string? ExcludedDirs { get; init; }

    /// <summary>
    /// 排除文件列表，换行分隔。
    /// </summary>
    [JsonPropertyName("excluded_files")]
    public string? ExcludedFiles { get; init; }

    /// <summary>
    /// 包含目录列表，换行分隔。
    /// </summary>
    [JsonPropertyName("included_dirs")]
    public string? IncludedDirs { get; init; }

    /// <summary>
    /// 包含文件列表，换行分隔。
    /// </summary>
    [JsonPropertyName("included_files")]
    public string? IncludedFiles { get; init; }
}

/// <summary>
/// Wiki 任务请求。
/// </summary>
public sealed class WikiTaskRequest : TaskRequestBase
{
    /// <summary>
    /// 是否生成综合版 Wiki。
    /// </summary>
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; init; } = true;

    /// <summary>
    /// 是否强制刷新并忽略缓存。
    /// </summary>
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; init; }
}

/// <summary>
/// Ask 任务请求。
/// </summary>
public sealed class AskTaskRequest : TaskRequestBase
{
    /// <summary>
    /// 当前问题。
    /// </summary>
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// 历史消息。
    /// </summary>
    [JsonPropertyName("history")]
    public List<ChatMessage> History { get; init; } = new();

    /// <summary>
    /// 是否启用深度研究。
    /// </summary>
    [JsonPropertyName("deep_research")]
    public bool DeepResearch { get; init; }

    /// <summary>
    /// 可选文件路径。
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }
}

/// <summary>
/// Slides 任务请求。
/// </summary>
public sealed class SlidesTaskRequest : TaskRequestBase
{
    /// <summary>
    /// 是否强制刷新 Wiki 依赖。
    /// </summary>
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; init; }

    /// <summary>
    /// 是否生成综合版 Wiki 作为输入。
    /// </summary>
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; init; } = true;
}

/// <summary>
/// Workshop 任务请求。
/// </summary>
public sealed class WorkshopTaskRequest : TaskRequestBase
{
    /// <summary>
    /// 是否强制刷新 Wiki 依赖。
    /// </summary>
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; init; }

    /// <summary>
    /// 是否生成综合版 Wiki 作为输入。
    /// </summary>
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; init; } = true;
}

/// <summary>
/// Ask 研究阶段。
/// </summary>
public sealed class AskResearchStage
{
    /// <summary>
    /// 阶段标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 阶段内容。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 轮次。
    /// </summary>
    [JsonPropertyName("iteration")]
    public int Iteration { get; init; }

    /// <summary>
    /// 阶段类型。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// Ask 任务响应。
/// </summary>
public sealed class AskTaskResponse
{
    /// <summary>
    /// 最终内容。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 研究阶段列表。
    /// </summary>
    [JsonPropertyName("stages")]
    public List<AskResearchStage> Stages { get; init; } = new();

    /// <summary>
    /// 是否已完成。
    /// </summary>
    [JsonPropertyName("complete")]
    public bool Complete { get; init; }

    /// <summary>
    /// 实际执行轮次。
    /// </summary>
    [JsonPropertyName("iterations")]
    public int Iterations { get; init; }
}

/// <summary>
/// Wiki 任务响应。
/// </summary>
public sealed class WikiTaskResponse
{
    /// <summary>
    /// 是否来自缓存。
    /// </summary>
    [JsonPropertyName("from_cache")]
    public bool FromCache { get; init; }

    /// <summary>
    /// 仓库信息。
    /// </summary>
    [JsonPropertyName("repo")]
    public RepoInfo Repo { get; init; } = new();

    /// <summary>
    /// 语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; init; } = "zh";

    /// <summary>
    /// 生效 provider。
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// 生效模型。
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Wiki 结构。
    /// </summary>
    [JsonPropertyName("wiki_structure")]
    public WikiStructure WikiStructure { get; init; } = new();

    /// <summary>
    /// 已生成页面。
    /// </summary>
    [JsonPropertyName("generated_pages")]
    public Dictionary<string, WikiPage> GeneratedPages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 调试信息
    /// </summary>
    [JsonPropertyName("debug")]
    public WikiTaskDebugInfo Debug { get; init; } = new();
}

/// <summary>
/// Wiki 任务调试信息
/// </summary>
public sealed class WikiTaskDebugInfo
{
    /// <summary>
    /// 请求标识
    /// </summary>
    [JsonPropertyName("request_id")]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// 仓库本地路径
    /// </summary>
    [JsonPropertyName("repository_path")]
    public string? RepositoryPath { get; init; }

    /// <summary>
    /// 仓库文件数量
    /// </summary>
    [JsonPropertyName("file_count")]
    public int FileCount { get; init; }

    /// <summary>
    /// 结构阶段页面数
    /// </summary>
    [JsonPropertyName("structure_page_count")]
    public int StructurePageCount { get; init; }

    /// <summary>
    /// 最终生成页面数
    /// </summary>
    [JsonPropertyName("generated_page_count")]
    public int GeneratedPageCount { get; init; }

    /// <summary>
    /// 是否使用后端兜底
    /// </summary>
    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; init; }

    /// <summary>
    /// 结构响应预览
    /// </summary>
    [JsonPropertyName("structure_response_preview")]
    public string? StructureResponsePreview { get; init; }

    /// <summary>
    /// 调试警告
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// 任务错误响应
/// </summary>
public sealed class TaskErrorResponse
{
    /// <summary>
    /// 错误摘要
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// 详细信息
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }

    /// <summary>
    /// 请求标识
    /// </summary>
    [JsonPropertyName("request_id")]
    public string RequestId { get; init; } = string.Empty;
}

/// <summary>
/// 单页幻灯片。
/// </summary>
public sealed class GeneratedSlide
{
    /// <summary>
    /// 标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 文本摘要。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// HTML 内容。
    /// </summary>
    [JsonPropertyName("html")]
    public string Html { get; init; } = string.Empty;
}

/// <summary>
/// Slides 任务响应。
/// </summary>
public sealed class SlidesTaskResponse
{
    /// <summary>
    /// 幻灯片规划文本。
    /// </summary>
    [JsonPropertyName("plan")]
    public string Plan { get; init; } = string.Empty;

    /// <summary>
    /// 幻灯片列表。
    /// </summary>
    [JsonPropertyName("slides")]
    public List<GeneratedSlide> Slides { get; init; } = new();
}

/// <summary>
/// Workshop 任务响应。
/// </summary>
public sealed class WorkshopTaskResponse
{
    /// <summary>
    /// Markdown 内容。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
