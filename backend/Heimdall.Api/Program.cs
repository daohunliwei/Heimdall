using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Api.Services.Auth;
using Heimdall.Api.Services.Cache;
using Heimdall.Api.Services.Chat;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Export;
using Heimdall.Api.Services.Projects;
using Heimdall.Api.Services.Providers;
using Heimdall.Api.Services.Rag;
using Heimdall.Api.Services.Repository;
using Heimdall.Api.Services.Streaming;
using Heimdall.Api.Services.SystemInfo;
using Heimdall.Api.Services.Tasks;
using Heimdall.Api.Services.Utility;

const string RuntimeConfigPathKey = "HEIMDALL_RUNTIME_CONFIG_PATH";

string? ResolveRuntimeConfigPath()
{
    var rawPath = Environment.GetEnvironmentVariable(RuntimeConfigPathKey);
    if (string.IsNullOrWhiteSpace(rawPath))
    {
        return null;
    }

    var fullPath = Path.GetFullPath(rawPath.Trim());
    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"环境变量 {RuntimeConfigPathKey} 指向的后端运行配置文件不存在：{fullPath}", fullPath);
    }

    return fullPath;
}

void ApplyRuntimeConfigFile(WebApplicationBuilder applicationBuilder, string[] commandLineArgs)
{
    var runtimeConfigPath = ResolveRuntimeConfigPath();
    if (string.IsNullOrWhiteSpace(runtimeConfigPath))
    {
        return;
    }

    applicationBuilder.Configuration.AddJsonFile(runtimeConfigPath, optional: false, reloadOnChange: false);
    applicationBuilder.Configuration.AddEnvironmentVariables();
    if (commandLineArgs.Length > 0)
    {
        applicationBuilder.Configuration.AddCommandLine(commandLineArgs);
    }
}

/// <summary>
/// 读取分钟配置并转换为 TimeSpan。
/// </summary>
TimeSpan ReadTimeoutFromMinutes(IConfiguration configuration, string key, double defaultMinutes)
{
    var raw = configuration[key];
    if (!string.IsNullOrWhiteSpace(raw) &&
        double.TryParse(raw.Trim(), out var minutes) &&
        minutes > 0)
    {
        return TimeSpan.FromMinutes(minutes);
    }

    return TimeSpan.FromMinutes(defaultMinutes);
}

var builder = WebApplication.CreateBuilder(args);
ApplyRuntimeConfigFile(builder, args);
var bootstrapConfig = builder.Configuration;

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddHttpClient(string.Empty, options =>
{
    options.Timeout = ReadTimeoutFromMinutes(bootstrapConfig, "HEIMDALL_HTTP_TIMEOUT_MINUTES", 180);
});

builder.Services.AddSingleton<HeimdallConfigService>();
builder.Services.AddSingleton<TextUtilityService>();
builder.Services.AddSingleton<PromptTemplateService>();
builder.Services.AddSingleton<TaskPromptService>();
builder.Services.AddSingleton<TaskRequestUtilityService>();
builder.Services.AddSingleton<TaskLlmService>();
builder.Services.AddSingleton<AuthorizationService>();
builder.Services.AddSingleton<SystemInfoService>();
builder.Services.AddSingleton<WikiExportService>();
builder.Services.AddSingleton<ChatStreamService>();
builder.Services.AddSingleton<WikiCacheService>();
builder.Services.AddSingleton<ProcessedProjectService>();
builder.Services.AddSingleton<RepositoryAccessService>(serviceProvider =>
    new RepositoryAccessService(
        serviceProvider.GetRequiredService<IConfiguration>(),
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<HeimdallConfigService>(),
        serviceProvider.GetRequiredService<ILogger<RepositoryAccessService>>(),
        serviceProvider.GetRequiredService<TextUtilityService>()));
builder.Services.AddSingleton<RepositoryEmbeddingService>();
builder.Services.AddSingleton<RagContextService>();
builder.Services.AddSingleton<ProviderRegistry>();
builder.Services.AddSingleton<ChatOrchestratorService>();
builder.Services.AddSingleton<WikiTaskService>();
builder.Services.AddSingleton<AskTaskService>();
builder.Services.AddSingleton<SlidesTaskService>();
builder.Services.AddSingleton<WorkshopTaskService>();
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new GoogleChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new MiniMaxChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new OpenAiCompatibleChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        "openai"));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new OpenAiCompatibleChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        "openrouter"));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new OpenAiCompatibleChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        "dashscope"));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new OllamaChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        serviceProvider.GetRequiredService<HeimdallConfigService>(),
        serviceProvider.GetRequiredService<ILogger<OllamaChatProvider>>()));
builder.Services.AddSingleton<IChatProvider>(serviceProvider =>
    new AzureChatProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider, BedrockChatProvider>();
builder.Services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
    new OpenAiEmbeddingProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
    new GoogleEmbeddingProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
    new OllamaEmbeddingProvider(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        serviceProvider.GetRequiredService<HeimdallConfigService>(),
        serviceProvider.GetRequiredService<ILogger<OllamaEmbeddingProvider>>()));
builder.Services.AddSingleton<IEmbeddingProvider, BedrockEmbeddingProvider>();

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();
