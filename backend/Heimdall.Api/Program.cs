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
using Heimdall.Core.Services.Auth;
using Heimdall.Core.Services.Cache;
using Heimdall.Core.Services.Rag;
using Heimdall.Core.Services.Tasks;
using Heimdall.Core.Services.Admin;
using Heimdall.Core.Services.Prompt;
using Heimdall.Core.Interfaces.Repositories;
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
builder.Services.AddScoped<IWikiRepository, WikiRepository>();
builder.Services.AddScoped<IWikiPageRepository, WikiPageRepository>();
builder.Services.AddScoped<ITaskLlmCallLogRepository, TaskLlmCallLogRepository>();
builder.Services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
builder.Services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
builder.Services.AddScoped<IRepositoryConfigRepository, RepositoryConfigRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

// Core Services (Scoped - 依赖 Repository)
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<WikiCacheService>();
builder.Services.AddScoped<RepositoryEmbeddingService>();
builder.Services.AddScoped<RagContextService>();
builder.Services.AddScoped<TaskRequestUtilityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PromptTemplateService>();

// Core Task Services
builder.Services.AddSingleton<TaskProgressService>();
builder.Services.AddSingleton<TaskLlmCallLogService>();
builder.Services.AddSingleton<TaskQueueService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskQueueService>());

// JWT Authentication
var authMode = bootstrapConfig[AuthModeKey] ?? "jwt";
if (!string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase))
{
    var jwtSecret = bootstrapConfig[JwtSecretKey];
    if (!string.IsNullOrWhiteSpace(jwtSecret))
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

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("EditorPlus", policy => policy.RequireRole("Admin", "Editor"));
        });
    }
}

var app = builder.Build();
app.UseCors();

if (!string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

// 确保数据库已创建
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
