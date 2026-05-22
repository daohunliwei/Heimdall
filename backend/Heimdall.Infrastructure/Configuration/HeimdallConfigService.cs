using System.Text.Json;
using System.Text.RegularExpressions;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Configuration;

/// <summary>
/// Heimdall 配置服务，负责加载 JSON 配置并处理环境变量占位符。
/// </summary>
public sealed class HeimdallConfigService
{
    private const string HeimdallConfigDirKey = "HEIMDALL_CONFIG_DIR";
    private const string HeimdallDefaultProviderKey = "HEIMDALL_DEFAULT_PROVIDER";
    private const string HeimdallEmbedderTypeKey = "HEIMDALL_EMBEDDER_TYPE";
    private const string HeimdallWikiTaskTimeoutKey = "HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES";
    private const string HeimdallOllamaTimeoutKey = "HEIMDALL_OLLAMA_REQUEST_TIMEOUT_MINUTES";
    private const string HeimdallHttpTimeoutKey = "HEIMDALL_HTTP_TIMEOUT_MINUTES";
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// 初始化配置服务。
    /// </summary>
    public HeimdallConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 内存缓存：Provider/Model → 元数据（由 Api 层通过 SetCachedMetadata 注入）。
    /// </summary>
    private Dictionary<string, ProviderModelMetadata>? _metadataOverrides;

    /// <summary>
    /// 由 Api 层注入数据库中的元数据覆盖值。调用后立即生效。
    /// </summary>
    public void SetMetadataOverrides(Dictionary<string, ProviderModelMetadata> overrides)
    {
        _metadataOverrides = overrides;
    }

    /// <summary>
    /// 清除元数据覆盖缓存。
    /// </summary>
    public void InvalidateMetadataCache()
    {
        _metadataOverrides = null;
    }

    /// <summary>
    /// 读取生成器配置。
    /// </summary>
    public GeneratorConfig GetGeneratorConfig()
    {
        return LoadJsonConfig<GeneratorConfig>("generator.json") ?? new GeneratorConfig();
    }

    /// <summary>
    /// 读取嵌入器配置。
    /// </summary>
    public EmbedderConfig GetEmbedderConfig()
    {
        return LoadJsonConfig<EmbedderConfig>("embedder.json") ?? new EmbedderConfig();
    }

    /// <summary>
    /// 读取语言配置。
    /// </summary>
    public LanguageConfig GetLanguageConfig()
    {
        return LoadJsonConfig<LanguageConfig>("lang.json") ?? new LanguageConfig();
    }

    /// <summary>
    /// 读取仓库配置。
    /// </summary>
    public RepoConfig GetRepoConfig()
    {
        return LoadJsonConfig<RepoConfig>("repo.json") ?? new RepoConfig();
    }

    /// <summary>
    /// 获取默认 Provider。
    /// </summary>
    public string GetDefaultProvider()
    {
        return (GetConfigurationValue(HeimdallDefaultProviderKey) ?? "ollama").Trim().ToLowerInvariant();
    }

    /// 获取当前嵌入器类型。
    /// </summary>
    public string GetEmbedderType()
    {
        return (GetConfigurationValue(HeimdallEmbedderTypeKey) ?? "ollama").Trim().ToLowerInvariant();
    }

    /// 获取认证模式。
    /// </summary>
    public string GetAuthMode()
    {
        return (GetConfigurationValue("HEIMDALL_AUTH_MODE") ?? "none").Trim().ToLowerInvariant();
    }

    /// 获取是否开放注册。
    /// </summary>
    public bool GetRegistrationOpen()
    {
        var v = GetConfigurationValue("HEIMDALL_REGISTRATION_OPEN");
        return v is null || v.Trim().ToLowerInvariant() is "true" or "1" or "yes";
    }

    /// <summary>
    /// 获取当前生效的嵌入器定义。
    /// </summary>
    public EmbedderEntryDefinition GetActiveEmbedder()
    {
        var embedderConfig = GetEmbedderConfig();
        return GetEmbedderType() switch
        {
            "ollama" => embedderConfig.EmbedderOllama,
            "google" => embedderConfig.EmbedderGoogle,
            "bedrock" => embedderConfig.EmbedderBedrock,
            _ => embedderConfig.Embedder
        };
    }

    /// <summary>
    /// 获取 Wiki 任务超时时间。
    /// </summary>
    public TimeSpan GetWikiTaskTimeout()
    {
        return GetTimeSpanFromMinutes(180, HeimdallWikiTaskTimeoutKey);
    }

    /// <summary>
    /// 获取 Ollama 请求超时时间。
    /// </summary>
    public TimeSpan GetOllamaRequestTimeout()
    {
        return GetTimeSpanFromMinutes(60, HeimdallOllamaTimeoutKey);
    }

    /// <summary>
    /// 获取 Provider 的自定义 endpoint URL。
    /// </summary>
    public string? GetProviderEndpoint(string providerId)
    {
        return _configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_ENDPOINT"];
    }

    /// <summary>
    /// 获取 Provider 的 API Key。
    /// </summary>
    public string? GetProviderApiKey(string providerId)
    {
        return _configuration[$"HEIMDALL_{providerId.ToUpperInvariant()}_API_KEY"];
    }

    /// <summary>
    /// 获取通用 HttpClient 超时时间。
    /// </summary>
    public TimeSpan GetHttpClientTimeout()
    {
        return GetTimeSpanFromMinutes(180, HeimdallHttpTimeoutKey);
    }

    /// <summary>
    /// 解析 Provider 配置并转换为前端接口响应。
    /// </summary>
    public ModelConfigResponse BuildModelConfigResponse()
    {
        var generatorConfig = GetGeneratorConfig();
        var providers = generatorConfig.Providers
            .Select(item => new ProviderConfig
            {
                Id = item.Key,
                Name = GetProviderDisplayName(item.Key),
                SupportsCustomModel = item.Value.SupportsCustomModel,
                Models = item.Value.Models.Keys
                    .Select(modelId => new ModelItem
                    {
                        Id = modelId,
                        Name = modelId
                    })
                    .ToList()
            })
            .ToList();

        var defaultProvider = GetConfigurationValue(HeimdallDefaultProviderKey) ?? generatorConfig.DefaultProvider;
        if (providers.All(provider => !string.Equals(provider.Id, defaultProvider, StringComparison.OrdinalIgnoreCase)))
        {
            defaultProvider = generatorConfig.DefaultProvider;
        }

        return new ModelConfigResponse
        {
            DefaultProvider = defaultProvider,
            Providers = providers
        };
    }

    /// <summary>
    /// 根据请求和配置解析最终 provider。
    /// </summary>
    public string ResolveProvider(ChatCompletionRequest request)
    {
        var generatorConfig = GetGeneratorConfig();
        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            return request.Provider.Trim().ToLowerInvariant();
        }

        var envDefault = GetConfigurationValue(HeimdallDefaultProviderKey);
        return !string.IsNullOrWhiteSpace(envDefault) ? envDefault.Trim().ToLowerInvariant() : generatorConfig.DefaultProvider;
    }

    /// <summary>
    /// 根据 provider 与请求解析最终模型。
    /// </summary>
    public string ResolveModel(ChatCompletionRequest request, string provider)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomModel))
        {
            return request.CustomModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            return request.Model.Trim();
        }

        var generatorConfig = GetGeneratorConfig();
        if (generatorConfig.Providers.TryGetValue(provider, out var definition))
        {
            return definition.DefaultModel;
        }

        throw new InvalidOperationException($"未找到 provider `{provider}` 的模型配置。");
    }

    /// <summary>
    /// 获取模型参数配置。
    /// </summary>
    public ProviderModelParameters GetProviderModelParameters(string provider, string model)
    {
        var generatorConfig = GetGeneratorConfig();
        if (generatorConfig.Providers.TryGetValue(provider, out var definition))
        {
            if (definition.Models.TryGetValue(model, out var parameters))
            {
                return parameters;
            }

            return new ProviderModelParameters();
        }

        throw new InvalidOperationException($"未找到 provider `{provider}` 的配置。");
    }

    /// <summary>
    /// 获取 Provider/Model 组合的计费与能力元数据。
    /// 优先从内存覆盖（DB）读取，其次回退到 generator.json。
    /// </summary>
    public ProviderModelMetadata GetProviderModelMetadata(string provider, string model)
    {
        if (_metadataOverrides != null && _metadataOverrides.TryGetValue($"{provider}/{model}", out var cached))
            return cached;

        var generatorConfig = GetGeneratorConfig();
        if (generatorConfig.Providers.TryGetValue(provider, out var definition) && definition.Metadata != null)
            return definition.Metadata;

        return InferDefaultMetadata(provider);
    }

    /// <summary>
    /// 获取上下文填充比例——优先使用模型元数据中的值，其次环境变量，最后默认 0.65。
    /// </summary>
    public double GetContextFillRatio(string? provider = null, string? model = null)
    {
        if (!string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(model))
        {
            var meta = GetProviderModelMetadata(provider, model);
            return meta.ContextFillRatio;
        }
        var raw = GetConfigurationValue("HEIMDALL_CONTEXT_FILL_RATIO");
        if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw.Trim(), out var ratio) && ratio is > 0.1 and <= 1.0)
            return ratio;
        return 0.65;
    }

    /// <summary>
    /// 根据 Provider 类型推断默认元数据。
    /// </summary>
    private static ProviderModelMetadata InferDefaultMetadata(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "ollama" => new ProviderModelMetadata
            {
                BillingType = BillingType.CodingPlan,
                MaxContextTokens = 131072,
                MaxOutputTokens = 32768,
                RateLimitPerMinute = 5,
                CallPrice = 0m,
                SupportsCaching = false
            },
            "openai" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 128000,
                MaxOutputTokens = 16384,
                InputTokenPrice = 2.50m,
                OutputTokenPrice = 10.00m,
                SupportsCaching = true
            },
            "google" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 1048576,
                MaxOutputTokens = 65536,
                InputTokenPrice = 0.15m,
                OutputTokenPrice = 0.60m,
                SupportsCaching = true
            },
            "azure" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 128000,
                MaxOutputTokens = 16384,
                InputTokenPrice = 2.50m,
                OutputTokenPrice = 10.00m,
                SupportsCaching = false
            },
            "minimax" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 1048576,
                MaxOutputTokens = 65536,
                InputTokenPrice = 1.00m,
                OutputTokenPrice = 4.00m,
                SupportsCaching = false
            },
            "dashscope" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 131072,
                MaxOutputTokens = 16384,
                InputTokenPrice = 0.80m,
                OutputTokenPrice = 2.00m,
                SupportsCaching = false
            },
            "bedrock" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 200000,
                MaxOutputTokens = 4096,
                InputTokenPrice = 3.00m,
                OutputTokenPrice = 15.00m,
                SupportsCaching = false
            },
            "openrouter" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 128000,
                MaxOutputTokens = 16384,
                InputTokenPrice = 2.50m,
                OutputTokenPrice = 10.00m,
                SupportsCaching = false
            },
            "deepseek" => new ProviderModelMetadata
            {
                BillingType = BillingType.TokenPlan,
                MaxContextTokens = 1048576,
                MaxOutputTokens = 384000,
                ContextFillRatio = 0.85,
                InputTokenPrice = 0.28m,
                OutputTokenPrice = 1.10m,
                SupportsCaching = true
            },
            _ => new ProviderModelMetadata()
        };
    }

    /// <summary>
    /// 读取 JSON 配置并执行环境变量替换。
    /// </summary>
    private T? LoadJsonConfig<T>(string fileName)
    {
        var configDirectory = GetConfigurationValue(HeimdallConfigDirKey);
        var baseDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "config")
            : configDirectory;
        var fullPath = Path.Combine(baseDirectory, fileName);

        if (!File.Exists(fullPath))
        {
            return default;
        }

        var json = File.ReadAllText(fullPath);
        json = ReplaceEnvironmentPlaceholders(json);
        return JsonSerializer.Deserialize<T>(json, _jsonSerializerOptions);
    }

    /// <summary>
    /// 替换配置中的环境变量占位符。
    /// </summary>
    private string ReplaceEnvironmentPlaceholders(string content)
    {
        return Regex.Replace(content, @"\$\{([A-Z0-9_]+)\}", match =>
        {
            var variableName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(variableName) ?? match.Value;
        });
    }

    /// <summary>
    /// 按优先级读取配置值。
    /// </summary>
    private string? GetConfigurationValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取 provider 中文展示名。
    /// </summary>
    private static string GetProviderDisplayName(string providerId)
    {
        return providerId switch
        {
            "google" => "Google",
            "minimax" => "MiniMax",
            "openai" => "OpenAI",
            "openrouter" => "OpenRouter",
            "ollama" => "Ollama（本地）",
            "bedrock" => "AWS Bedrock",
            "azure" => "Azure AI",
            "dashscope" => "DashScope",
            "deepseek" => "DeepSeek",
            _ => providerId
        };
    }

    /// <summary>
    /// 读取分钟配置并转换为 TimeSpan。
    /// </summary>
    private TimeSpan GetTimeSpanFromMinutes(double defaultMinutes, params string[] keys)
    {
        var raw = GetConfigurationValue(keys);
        if (!string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw.Trim(), out var minutes) &&
            minutes > 0)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return TimeSpan.FromMinutes(defaultMinutes);
    }
}
