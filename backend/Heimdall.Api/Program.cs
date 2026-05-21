using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Providers.ChatProviders;
using Heimdall.Infrastructure.Providers.EmbeddingProviders;
using Heimdall.Infrastructure.RepositorySources;
using Heimdall.Infrastructure.Utilities;
using Heimdall.Api.Middleware;
using Heimdall.Core.Services.Auth;
using Heimdall.Core.Services.Rag;
using Heimdall.Core.Services.Tasks;
using Heimdall.Core.Services.Admin;
using Heimdall.Core.Services.Prompt;
using Heimdall.Core.Services.Repository;
using Heimdall.Core.Interfaces;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Repository.Data;
using Heimdall.Repository.Repositories;

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

// PostgreSQL / EF Core
var connectionString = bootstrapConfig[ConnectionStringKey]
    ?? "Host=localhost;Port=5432;Database=heimdall;Username=heimdall;Password=heimdall";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Infrastructure Layer (Singleton - 无状态)
builder.Services.AddSingleton<HeimdallConfigService>();
builder.Services.AddSingleton<TextUtilityService>();
builder.Services.AddSingleton<ProviderRegistry>();

// Repository Sources
builder.Services.AddSingleton<IRepositorySource, GitHubRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, GitLabRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, BitbucketRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, LocalDirectorySource>();

// Chat Providers
builder.Services.AddSingleton<IChatProvider>(sp =>
    new GoogleChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new MiniMaxChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new OpenAiCompatibleChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>(), "openai"));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new OpenAiCompatibleChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>(), "openrouter"));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new OpenAiCompatibleChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>(), "dashscope"));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new OllamaChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<HeimdallConfigService>(), sp.GetRequiredService<ILogger<OllamaChatProvider>>()));
builder.Services.AddSingleton<IChatProvider>(sp =>
    new AzureChatProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IChatProvider, BedrockChatProvider>();

// Embedding Providers
builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
    new OpenAiEmbeddingProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
    new GoogleEmbeddingProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
    new OllamaEmbeddingProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<HeimdallConfigService>(), sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>()));
builder.Services.AddSingleton<IEmbeddingProvider, BedrockEmbeddingProvider>();

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
builder.Services.AddScoped<ICodeEmbeddingRepository, CodeEmbeddingRepository>();
builder.Services.AddScoped<IWikiEmbeddingRepository, WikiEmbeddingRepository>();
builder.Services.AddScoped<IWikiSpaceRepository, WikiSpaceRepository>();
builder.Services.AddScoped<IWikiVersionRepository, WikiVersionRepository>();
builder.Services.AddScoped<IWikiPageRelationRepository, WikiPageRelationRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
builder.Services.AddScoped<IProviderMetadataRepository, ProviderMetadataRepository>();

// Core Services (Scoped - 依赖 Repository)
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IVersionDiscoveryService, VersionDiscoveryService>();
builder.Services.AddScoped<IRefreshOrchestrationService, RefreshOrchestrationService>();
builder.Services.AddScoped<IDualVectorSearchService, DualVectorSearchService>();
builder.Services.AddScoped<ICodeEmbeddingService, CodeEmbeddingService>();
builder.Services.AddScoped<IWikiEmbeddingService, WikiEmbeddingService>();
builder.Services.AddScoped<IVersionedKnowledgeService, VersionedKnowledgeService>();
builder.Services.AddScoped<IAskTaskService, AskTaskService>();
builder.Services.AddScoped<ISlidesTaskService, SlidesTaskService>();
builder.Services.AddScoped<IWorkshopTaskService, WorkshopTaskService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RagContextService>();
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
builder.Services.AddSingleton<Heimdall.Infrastructure.Services.ProviderRateLimiter>();
builder.Services.AddSingleton<Heimdall.Infrastructure.Services.LlmRetryPolicy>();
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

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 确保数据库已创建并初始化种子数据
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // 确保 provider_model_metadata 表存在（迁移未通过 dotnet ef 执行时的回退）
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS provider_model_metadata (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"ProviderKey\" varchar(64) NOT NULL, " +
            "\"ModelName\" varchar(128) NOT NULL, " +
            "\"BillingType\" varchar(32) NOT NULL DEFAULT 'TokenPlan', " +
            "\"MaxContextTokens\" integer NOT NULL DEFAULT 128000, " +
            "\"MaxOutputTokens\" integer NOT NULL DEFAULT 8192, " +
            "\"RateLimitPerMinute\" integer NULL, " +
            "\"InputTokenPrice\" numeric(10,6) NULL, " +
            "\"OutputTokenPrice\" numeric(10,6) NULL, " +
            "\"CallPrice\" numeric(10,6) NULL, " +
            "\"SupportsCaching\" boolean NOT NULL DEFAULT FALSE, " +
            "\"ContextFillRatio\" double precision NOT NULL DEFAULT 0.65, " +
            "\"ContextWarningThreshold\" double precision NOT NULL DEFAULT 0.90, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL DEFAULT NOW())");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_provider_model_metadata_key_model ON provider_model_metadata (\"ProviderKey\", \"ModelName\")");
    }
    catch { /* 表已存在则跳过 */ }

    var seedData = scope.ServiceProvider.GetRequiredService<Heimdall.Core.Services.Prompt.PromptSeedData>();
    await seedData.SeedAsync();
}

app.Run();
