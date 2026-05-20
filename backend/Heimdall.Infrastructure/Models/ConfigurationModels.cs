using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Infrastructure.Models;

public class ModelConfigResponse
{
    public string DefaultProvider { get; set; } = "google";
    public List<ProviderConfig> Providers { get; set; } = new();
}

public class ProviderConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool SupportsCustomModel { get; set; }
    public List<ModelItem> Models { get; set; } = new();
}

public class ModelItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ProviderModelParameters
{
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public Dictionary<string, JsonElement>? Options { get; set; }
}

public class ProviderDefinition
{
    [JsonPropertyName("default_model")]
    public string DefaultModel { get; set; } = string.Empty;
    public bool SupportsCustomModel { get; set; }
    public Dictionary<string, ProviderModelParameters> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Provider 级别的计费与能力元数据（可选，未配置时使用默认值）。</summary>
    public ProviderModelMetadata? Metadata { get; set; }
}

public class GeneratorConfig
{
    [JsonPropertyName("default_provider")]
    public string DefaultProvider { get; set; } = "google";
    public Dictionary<string, ProviderDefinition> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class EmbedderEntryDefinition
{
    public string ClientClass { get; set; } = string.Empty;
    public int? BatchSize { get; set; }
    public Dictionary<string, JsonElement> ModelKwargs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Host { get; set; } = string.Empty;
}

public class RetrieverDefinition
{
    public int TopK { get; set; } = 20;
}

public class TextSplitterDefinition
{
    public string SplitBy { get; set; } = "word";
    public int ChunkSize { get; set; } = 350;
    public int ChunkOverlap { get; set; } = 100;
}

public class EmbedderConfig
{
    public EmbedderEntryDefinition Embedder { get; set; } = new();
    public EmbedderEntryDefinition EmbedderOllama { get; set; } = new();
    public EmbedderEntryDefinition EmbedderGoogle { get; set; } = new();
    public EmbedderEntryDefinition EmbedderBedrock { get; set; } = new();
    public RetrieverDefinition Retriever { get; set; } = new();
    public TextSplitterDefinition TextSplitter { get; set; } = new();
}

public class FileFilterDefinition
{
    public List<string> ExcludedDirs { get; set; } = new();
    public List<string> ExcludedFiles { get; set; } = new();
}

public class RepositoryDefinition
{
    public int MaxSizeMb { get; set; } = 50000;
}

public class RepoConfig
{
    public FileFilterDefinition FileFilters { get; set; } = new();
    public RepositoryDefinition Repository { get; set; } = new();
}

public class LanguageConfig
{
    public Dictionary<string, string> SupportedLanguages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Default { get; set; } = "en";
}
