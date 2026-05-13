using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 聊天消息。
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// 消息角色。
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// 消息正文。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// 流式聊天请求体。
/// </summary>
public sealed class ChatCompletionRequest
{
    /// <summary>
    /// 仓库地址或本地目录。
    /// </summary>
    [JsonPropertyName("repo_url")]
    public string RepoUrl { get; init; } = string.Empty;

    /// <summary>
    /// 历史消息列表。
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; init; } = new();

    /// <summary>
    /// 聚焦文件路径。
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    /// <summary>
    /// 私有仓库访问令牌。
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>
    /// 仓库类型。
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

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
/// 模型调用请求。
/// </summary>
public sealed class ProviderChatRequest
{
    /// <summary>
    /// Provider 标识。
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// 模型名称。
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 提示词。
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 温度。
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Top P。
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Top K。
    /// </summary>
    public int? TopK { get; init; }

    /// <summary>
    /// 额外 options。
    /// </summary>
    public Dictionary<string, JsonElement>? Options { get; init; }
}
