using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 模型配置响应。
/// </summary>
public sealed class ModelConfigResponse
{
    /// <summary>
    /// 默认提供方。
    /// </summary>
    [JsonPropertyName("defaultProvider")]
    public string DefaultProvider { get; init; } = "google";

    /// <summary>
    /// Provider 列表。
    /// </summary>
    [JsonPropertyName("providers")]
    public List<ProviderConfig> Providers { get; init; } = new();
}

/// <summary>
/// Provider 配置。
/// </summary>
public sealed class ProviderConfig
{
    /// <summary>
    /// Provider 标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Provider 名称。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否支持自定义模型。
    /// </summary>
    [JsonPropertyName("supportsCustomModel")]
    public bool SupportsCustomModel { get; init; }

    /// <summary>
    /// 模型列表。
    /// </summary>
    [JsonPropertyName("models")]
    public List<ModelItem> Models { get; init; } = new();
}

/// <summary>
/// 模型条目。
/// </summary>
public sealed class ModelItem
{
    /// <summary>
    /// 模型标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 模型名称。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Provider 模型参数。
/// </summary>
public sealed class ProviderModelParameters
{
    /// <summary>
    /// 温度。
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    /// <summary>
    /// Top P。
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    /// <summary>
    /// Top K。
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; init; }

    /// <summary>
    /// Ollama options。
    /// </summary>
    [JsonPropertyName("options")]
    public Dictionary<string, JsonElement>? Options { get; init; }
}

/// <summary>
/// Provider 原始配置。
/// </summary>
public sealed class ProviderDefinition
{
    /// <summary>
    /// 默认模型。
    /// </summary>
    [JsonPropertyName("default_model")]
    public string DefaultModel { get; init; } = string.Empty;

    /// <summary>
    /// 是否支持自定义模型。
    /// </summary>
    [JsonPropertyName("supportsCustomModel")]
    public bool SupportsCustomModel { get; init; }

    /// <summary>
    /// 模型参数表。
    /// </summary>
    [JsonPropertyName("models")]
    public Dictionary<string, ProviderModelParameters> Models { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 生成器配置。
/// </summary>
public sealed class GeneratorConfig
{
    /// <summary>
    /// 默认 Provider。
    /// </summary>
    [JsonPropertyName("default_provider")]
    public string DefaultProvider { get; init; } = "google";

    /// <summary>
    /// Provider 集合。
    /// </summary>
    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderDefinition> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 嵌入器条目配置。
/// </summary>
public sealed class EmbedderEntryDefinition
{
    /// <summary>
    /// 客户端类型名称。
    /// </summary>
    [JsonPropertyName("client_class")]
    public string ClientClass { get; init; } = string.Empty;

    /// <summary>
    /// 批量大小。
    /// </summary>
    [JsonPropertyName("batch_size")]
    public int? BatchSize { get; init; }

    /// <summary>
    /// 模型参数。
    /// </summary>
    [JsonPropertyName("model_kwargs")]
    public Dictionary<string, JsonElement> ModelKwargs { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 服务地址。
    /// </summary>
    [JsonPropertyName("host")]
    public string Host { get; init; } = string.Empty;
}

/// <summary>
/// 检索器配置。
/// </summary>
public sealed class RetrieverDefinition
{
    /// <summary>
    /// 检索返回数量。
    /// </summary>
    [JsonPropertyName("top_k")]
    public int TopK { get; init; } = 20;
}

/// <summary>
/// 文本切分配置。
/// </summary>
public sealed class TextSplitterDefinition
{
    /// <summary>
    /// 切分方式。
    /// </summary>
    [JsonPropertyName("split_by")]
    public string SplitBy { get; init; } = "word";

    /// <summary>
    /// 分块大小。
    /// </summary>
    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; init; } = 350;

    /// <summary>
    /// 重叠词数。
    /// </summary>
    [JsonPropertyName("chunk_overlap")]
    public int ChunkOverlap { get; init; } = 100;
}

/// <summary>
/// 嵌入器配置总表。
/// </summary>
public sealed class EmbedderConfig
{
    /// <summary>
    /// 默认 OpenAI 嵌入器。
    /// </summary>
    [JsonPropertyName("embedder")]
    public EmbedderEntryDefinition Embedder { get; init; } = new();

    /// <summary>
    /// Ollama 嵌入器。
    /// </summary>
    [JsonPropertyName("embedder_ollama")]
    public EmbedderEntryDefinition EmbedderOllama { get; init; } = new();

    /// <summary>
    /// Google 嵌入器。
    /// </summary>
    [JsonPropertyName("embedder_google")]
    public EmbedderEntryDefinition EmbedderGoogle { get; init; } = new();

    /// <summary>
    /// Bedrock 嵌入器。
    /// </summary>
    [JsonPropertyName("embedder_bedrock")]
    public EmbedderEntryDefinition EmbedderBedrock { get; init; } = new();

    /// <summary>
    /// 检索器配置。
    /// </summary>
    [JsonPropertyName("retriever")]
    public RetrieverDefinition Retriever { get; init; } = new();

    /// <summary>
    /// 切分器配置。
    /// </summary>
    [JsonPropertyName("text_splitter")]
    public TextSplitterDefinition TextSplitter { get; init; } = new();
}

/// <summary>
/// 文件过滤配置。
/// </summary>
public sealed class FileFilterDefinition
{
    /// <summary>
    /// 默认排除目录。
    /// </summary>
    [JsonPropertyName("excluded_dirs")]
    public List<string> ExcludedDirs { get; init; } = new();

    /// <summary>
    /// 默认排除文件。
    /// </summary>
    [JsonPropertyName("excluded_files")]
    public List<string> ExcludedFiles { get; init; } = new();
}

/// <summary>
/// 仓库限制配置。
/// </summary>
public sealed class RepositoryDefinition
{
    /// <summary>
    /// 允许的最大仓库大小（MB）。
    /// </summary>
    [JsonPropertyName("max_size_mb")]
    public int MaxSizeMb { get; init; } = 50000;
}

/// <summary>
/// 仓库配置总表。
/// </summary>
public sealed class RepoConfig
{
    /// <summary>
    /// 文件过滤配置。
    /// </summary>
    [JsonPropertyName("file_filters")]
    public FileFilterDefinition FileFilters { get; init; } = new();

    /// <summary>
    /// 仓库限制。
    /// </summary>
    [JsonPropertyName("repository")]
    public RepositoryDefinition Repository { get; init; } = new();
}

/// <summary>
/// 语言配置总表。
/// </summary>
public sealed class LanguageConfig
{
    /// <summary>
    /// 支持的语言。
    /// </summary>
    [JsonPropertyName("supported_languages")]
    public Dictionary<string, string> SupportedLanguages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 默认语言。
    /// </summary>
    [JsonPropertyName("default")]
    public string Default { get; init; } = "en";
}
