using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Providers.CustomBackends;
using Heimdall.Infrastructure.RepositorySources;
using Heimdall.Infrastructure.Utilities;
using Heimdall.Api.Middleware;
using Heimdall.Core.Services.Auth;
using Heimdall.Core.Services.Tasks;
using Heimdall.Core.Services.Admin;
using Heimdall.Core.Services.Prompt;
using Heimdall.Core.Services.Repository;
using Heimdall.Core.Interfaces;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Repository.Repositories;
using SqlSugar;
using Heimdall.Core.Entities;
using Heimdall.Core.Services;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

const string RuntimeConfigPathKey = "HEIMDALL_RUNTIME_CONFIG_PATH";
const string ConnectionStringKey = "HEIMDALL_CONNECTION_STRING";
const string AuthModeKey = "HEIMDALL_AUTH_MODE";
const string JwtSecretKey = "HEIMDALL_JWT_SECRET";

string? ResolveRuntimeConfigPath()
{
    var rawPath = Environment.GetEnvironmentVariable(RuntimeConfigPathKey);
    if (string.IsNullOrWhiteSpace(rawPath)) return null;
    var fullPath = Path.GetFullPath(rawPath.Trim());
    if (!File.Exists(fullPath))
        throw new FileNotFoundException($"环境变量 {RuntimeConfigPathKey} 指向的后端运行配置文件不存在：{fullPath}", fullPath);
    return fullPath;
}

void ApplyRuntimeConfigFile(WebApplicationBuilder applicationBuilder, string[] commandLineArgs)
{
    var runtimeConfigPath = ResolveRuntimeConfigPath();
    if (string.IsNullOrWhiteSpace(runtimeConfigPath)) return;
    applicationBuilder.Configuration.AddJsonFile(runtimeConfigPath, optional: false, reloadOnChange: false);
    applicationBuilder.Configuration.AddEnvironmentVariables();
    if (commandLineArgs.Length > 0) applicationBuilder.Configuration.AddCommandLine(commandLineArgs);
}

TimeSpan ReadTimeoutFromMinutes(IConfiguration configuration, string key, double defaultMinutes)
{
    var raw = configuration[key];
    if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw.Trim(), out var minutes) && minutes > 0)
        return TimeSpan.FromMinutes(minutes);
    return TimeSpan.FromMinutes(defaultMinutes);
}

var builder = WebApplication.CreateBuilder(args);

// 日志：始终输出到控制台
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "simple";
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// 动态日志过滤器 — 运行时控制 SQL / EF Core 日志开关
var logCategoryFilter = new Heimdall.Infrastructure.Logging.LogCategoryFilter();
var logSqlEnv = builder.Configuration.GetValue<string>("HEIMDALL_LOG_SQL");
if (string.Equals(logSqlEnv, "true", StringComparison.OrdinalIgnoreCase) || logSqlEnv == "1")
{
    logCategoryFilter.ShowSqlCommands = true;
}
builder.Services.AddSingleton(logCategoryFilter);
builder.Services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<Microsoft.Extensions.Logging.LoggerFilterOptions>,
    Heimdall.Infrastructure.Logging.DynamicLogFilterOptions>();

ApplyRuntimeConfigFile(builder, args);
var bootstrapConfig = builder.Configuration;

// JSON 配置
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = true;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// HttpClient
builder.Services.AddHttpClient(string.Empty, options =>
{
    options.Timeout = ReadTimeoutFromMinutes(bootstrapConfig, "HEIMDALL_HTTP_TIMEOUT_MINUTES", 180);
});

// ===== SqlSugar ORM 配置 =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("HEIMDALL_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=heimdall;Username=postgres;Password=postgres";

var sqlSugarScope = new SqlSugarScope(new ConnectionConfig
{
    DbType = DbType.PostgreSQL,
    ConnectionString = connectionString,
    IsAutoCloseConnection = true,
    InitKeyType = InitKeyType.Attribute,
    ConfigureExternalServices = new ConfigureExternalServices
    {
        EntityNameService = (type, entity) =>
        {
            if (!type.Namespace!.Contains("Dto"))
            {
                entity.DbTableName = SqlSugar.UtilMethods.ToUnderLine(entity.DbTableName);
            }
        }
    },
    AopEvents = new AopEvents
    {
        OnLogExecuting = (sql, pars) =>
        {
            Console.WriteLine($"[SqlSugar] {sql}");
        },
        OnLogExecuted = (sql, pars) =>
        {
            Console.WriteLine("[SqlSugar] SQL 执行完成");
        }
    }
});

sqlSugarScope.DbMaintenance.CreateDatabase();

// 注册 SqlSugarScope 为单例
builder.Services.AddSingleton<ISqlSugarClient>(sqlSugarScope);
builder.Services.AddSingleton(sqlSugarScope);
builder.Services.AddSingleton<CodeFirstSyncService>();

// Infrastructure Layer (Singleton - 无状态)
builder.Services.AddSingleton<HeimdallConfigService>();
builder.Services.AddSingleton<TextUtilityService>();
builder.Services.AddSingleton<ChatClientFactory>();

// Repository Sources
builder.Services.AddSingleton<IRepositorySource, GitHubRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, GitLabRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, BitbucketRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, LocalDirectorySource>();

// MEAI Chat Clients — 使用 Keyed Service 按 Provider ID 注册
var config = builder.Configuration;

// OpenAI 兼容 Provider（5 个 → 1 个工厂）
builder.Services.AddKeyedSingleton<IChatClient>("openai",
    (sp, _) => OpenAiCompatibleClientFactory.Create(sp.GetRequiredService<IConfiguration>(), "openai", config["HEIMDALL_OPENAI_MODEL"] ?? "gpt-4o"));
builder.Services.AddKeyedSingleton<IChatClient>("openrouter",
    (sp, _) => OpenAiCompatibleClientFactory.Create(sp.GetRequiredService<IConfiguration>(), "openrouter", config["HEIMDALL_OPENROUTER_MODEL"] ?? "openai/gpt-4o"));
builder.Services.AddKeyedSingleton<IChatClient>("dashscope",
    (sp, _) => OpenAiCompatibleClientFactory.Create(sp.GetRequiredService<IConfiguration>(), "dashscope", config["HEIMDALL_DASHSCOPE_MODEL"] ?? "qwen-plus"));
builder.Services.AddKeyedSingleton<IChatClient>("deepseek",
    (sp, _) => OpenAiCompatibleClientFactory.Create(sp.GetRequiredService<IConfiguration>(), "deepseek", config["HEIMDALL_DEEPSEEK_MODEL"] ?? "deepseek-chat"));

// Azure OpenAI — 使用 OpenAI 兼容 API
builder.Services.AddKeyedSingleton<IChatClient>("azure", (sp, _) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var endpoint = cfg["HEIMDALL_AZURE_ENDPOINT"] ?? "";
    var apiKey = cfg["HEIMDALL_AZURE_API_KEY"] ?? "";
    var deployment = cfg["HEIMDALL_AZURE_DEPLOYMENT"] ?? "gpt-4o";
    return OpenAiCompatibleClientFactory.CreateAzure(cfg, "azure", deployment);
});

// AWS Bedrock
builder.Services.AddKeyedSingleton<IChatClient>("bedrock", (sp, _) =>
    BedrockClientFactory.Create(sp.GetRequiredService<IConfiguration>(),
        config["HEIMDALL_BEDROCK_MODEL"] ?? "anthropic.claude-sonnet-4-20250514-v1:0",
        sp.GetRequiredService<ILoggerFactory>()));

// Ollama
builder.Services.AddKeyedSingleton<IChatClient>("ollama", (sp, _) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var host = cfg["HEIMDALL_OLLAMA_CHAT_HOST"] ?? "http://127.0.0.1:11434";
    var model = cfg["HEIMDALL_OLLAMA_MODEL"] ?? "qwen3:32b";
    return new Heimdall.Infrastructure.Providers.CustomBackends.OllamaChatClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(), host, model,
        sp.GetRequiredService<ILogger<Heimdall.Infrastructure.Providers.CustomBackends.OllamaChatClient>>());
});

// Google Gemini
builder.Services.AddKeyedSingleton<IChatClient>("google", (sp, _) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var apiKey = cfg["HEIMDALL_GOOGLE_API_KEY"] ?? "";
    var model = cfg["HEIMDALL_GOOGLE_MODEL"] ?? "gemini-2.5-pro";
    return new Heimdall.Infrastructure.Providers.CustomBackends.GeminiChatClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(), apiKey, model,
        sp.GetRequiredService<ILogger<Heimdall.Infrastructure.Providers.CustomBackends.GeminiChatClient>>());
});

// MiniMax
builder.Services.AddKeyedSingleton<IChatClient>("minimax", (sp, _) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var apiKey = cfg["HEIMDALL_MINIMAX_API_KEY"] ?? "";
    var model = cfg["HEIMDALL_MINIMAX_MODEL"] ?? "MiniMax-Text-01";
    return new Heimdall.Infrastructure.Providers.CustomBackends.MiniMaxChatClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(), apiKey, model,
        sp.GetRequiredService<ILogger<Heimdall.Infrastructure.Providers.CustomBackends.MiniMaxChatClient>>());
});

// V8: 嵌入提供器已移除——当前阶段不需要向量化

// Repository Layer (Scoped - 依赖 DbContext)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskArtifactRepository, TaskArtifactRepository>();
builder.Services.AddScoped<IWikiTaskExecutionRepository, WikiTaskExecutionRepository>();
// V4 清理：IWikiRepository 随旧 Wiki 实体一并移除，Wiki 数据走 IWikiVersionRepository + IWikiPageRepository
builder.Services.AddScoped<IWikiPageRepository, WikiPageRepository>();
builder.Services.AddScoped<ITaskLlmCallLogRepository, TaskLlmCallLogRepository>();
builder.Services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
builder.Services.AddScoped<IPromptOverrideRepository, PromptOverrideRepository>();
builder.Services.AddScoped<IPromptTemplateHistoryRepository, PromptTemplateHistoryRepository>();
builder.Services.AddScoped<IRepositoryConfigRepository, RepositoryConfigRepository>();
builder.Services.AddScoped<IRepositoryVersionRepository, RepositoryVersionRepository>();
// V8: ICodeEmbeddingRepository / IWikiEmbeddingRepository 已移除
builder.Services.AddScoped<IWikiSpaceRepository, WikiSpaceRepository>();
builder.Services.AddScoped<IWikiVersionRepository, WikiVersionRepository>();
builder.Services.AddScoped<IWikiPageRelationRepository, WikiPageRelationRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
builder.Services.AddScoped<IProviderMetadataRepository, ProviderMetadataRepository>();

// Core Services (Scoped - 依赖 Repository)
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IVersionDiscoveryService, VersionDiscoveryService>();
builder.Services.AddScoped<IRefreshOrchestrationService, RefreshOrchestrationService>();
// V8: DualVectorSearchService / CodeEmbeddingService / WikiEmbeddingService 已移除——BM25 替代
builder.Services.AddScoped<IVersionedKnowledgeService, VersionedKnowledgeService>();
builder.Services.AddScoped<IAskTaskService, AskTaskService>();
builder.Services.AddScoped<ISlidesTaskService, SlidesTaskService>();
builder.Services.AddScoped<IWorkshopTaskService, WorkshopTaskService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtTokenService>();
// V8: RagContextService 已移除——Ask 使用 BM25 检索替代向量检索
builder.Services.AddScoped<TaskRequestUtilityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<TaskLlmCallLogService>();
builder.Services.AddScoped<IWikiTaskSubmissionService, WikiTaskSubmissionService>();
builder.Services.AddSingleton<RepositoryAccessService>();
builder.Services.AddSingleton<TaskLlmService>();
builder.Services.AddSingleton<Heimdall.Core.Interfaces.Services.IStructuredLogger, Heimdall.Core.Services.Logging.StructuredLogger>();
builder.Services.AddSingleton<TaskPromptService>();
builder.Services.AddSingleton<WikiGenerationParserService>();
builder.Services.AddSingleton<WikiGlobalConvergenceService>();
builder.Services.AddSingleton<WikiRenderPostProcessor>();
builder.Services.AddSingleton<WikiTaskService>();
builder.Services.AddScoped<PromptTemplateService>();
builder.Services.AddScoped<Heimdall.Core.Services.Prompt.PromptManagementService>();
builder.Services.AddSingleton<Heimdall.Core.Interfaces.Services.IPromptMergeService, Heimdall.Core.Services.Prompt.PromptMergeService>();
builder.Services.AddScoped<Heimdall.Core.Services.Prompt.PromptSeedData>();
builder.Services.AddSingleton<Heimdall.Core.Services.Repository.CodeStructureIndexService>();
builder.Services.AddSingleton<Heimdall.Core.Services.Repository.CodeIndexService>();
builder.Services.AddSingleton<Heimdall.Infrastructure.AstAnalysis.IAstAnalyzer, Heimdall.Infrastructure.AstAnalysis.RoslynCSharpAnalyzer>();
builder.Services.AddSingleton<Heimdall.Infrastructure.Search.Bm25SearchService>();
builder.Services.AddSingleton<Heimdall.Core.Interfaces.Services.IHybridSearchService, Heimdall.Core.Services.Search.HybridSearchService>();
builder.Services.AddScoped<Heimdall.Core.Interfaces.Repositories.ICodeIndexRepository, Heimdall.Repository.Repositories.CodeIndexRepository>();

// V7: LLM 可观测性与计费策略
builder.Services.AddScoped<Heimdall.Core.Interfaces.Repositories.ILlmMetricsRepository, Heimdall.Repository.Repositories.LlmMetricsRepository>();
builder.Services.AddScoped<Heimdall.Core.Interfaces.Services.ILlmObservabilityService, Heimdall.Core.Services.LlmObservabilityService>();
builder.Services.AddSingleton<Heimdall.Infrastructure.Services.ContextPackingService>();
builder.Services.AddSingleton<Heimdall.Infrastructure.Services.BillingStrategyService>();

// V7: 深度代码理解
builder.Services.AddSingleton<Heimdall.Core.Services.Repository.CallGraphBuilder>();
builder.Services.AddSingleton<Heimdall.Core.Services.Repository.DependencyTopologyService>();
builder.Services.AddSingleton<Heimdall.Core.Services.Repository.DesignPatternDetector>();
builder.Services.AddSingleton<Heimdall.Core.Interfaces.Services.ICodeUnderstandingService, Heimdall.Core.Services.Repository.CodeUnderstandingService>();

// Core Task Services (Singleton - 无状态或使用 IServiceScopeFactory)
builder.Services.AddSingleton<Heimdall.Core.Services.Tasks.AgentOrchestratorService>();
builder.Services.AddSingleton<Heimdall.Core.Services.Tasks.CostEstimationService>();
builder.Services.Configure<Heimdall.Core.Models.ModelTierConfig>(
    builder.Configuration.GetSection("ModelTier"));
builder.Services.AddSingleton<TaskProgressService>();
builder.Services.AddSingleton<TaskQueueService>();
builder.Services.AddSingleton<ITaskQueueService>(sp => sp.GetRequiredService<TaskQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskQueueService>());

// JWT Authentication
var authMode = bootstrapConfig[AuthModeKey] ?? "jwt";
var jwtSecret = bootstrapConfig[JwtSecretKey];
var useJwt = !string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(jwtSecret);

if (useJwt)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = "heimdall",
                ValidateAudience = true,
                ValidAudience = "heimdall",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
}
else
{
    // 无认证模式：注册空认证方案，避免 [Authorize] 属性抛出异常
    builder.Services.AddAuthentication("None")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoOpAuthHandler>("None", null);
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true).Build();
        options.AddPolicy("AdminOnly", policy => policy.RequireAssertion(_ => true));
        options.AddPolicy("EditorPlus", policy => policy.RequireAssertion(_ => true));
    });
}

if (useJwt)
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("EditorPlus", policy => policy.RequireRole("Admin", "Editor"));
    });
}

builder.Services.AddHostedService<Heimdall.Api.Services.ProviderMetadataStartupLoader>();
builder.Services.AddHostedService<TaskResumeService>();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// CodeFirst 自动同步
var codeFirstAutoSync = builder.Configuration.GetValue<bool>("CodeFirst:AutoSync");
var envAutoSync = Environment.GetEnvironmentVariable("HEIMDALL_CODEFIRST_AUTOSYNC");
if (!string.IsNullOrEmpty(envAutoSync))
{
    bool.TryParse(envAutoSync, out codeFirstAutoSync);
}

if (codeFirstAutoSync)
{
    try
    {
        var db = app.Services.GetRequiredService<ISqlSugarClient>();
        var codeFirstSyncService = app.Services.GetRequiredService<CodeFirstSyncService>();
        await codeFirstSyncService.SyncAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "CodeFirst 自动同步失败，请手动执行 SQL 脚本");
    }
}

// 启动时自动执行种子数据
using (var scope = app.Services.CreateScope())
{
    var seedData = scope.ServiceProvider.GetRequiredService<Heimdall.Core.Services.Prompt.PromptSeedData>();
    await seedData.SeedAsync();
}

app.Run();
