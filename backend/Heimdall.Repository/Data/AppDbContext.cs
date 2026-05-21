using Heimdall.Core.Entities;
using Heimdall.Repository.Data.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 抑制待定模型变更警告——数据库初始化时表结构可能通过其他方式补齐
        optionsBuilder.ConfigureWarnings(w => w.Ignore(
            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Core.Entities.Repository> Repositories => Set<Core.Entities.Repository>();
    public DbSet<TaskRecord> Tasks => Set<TaskRecord>();
    public DbSet<TaskArtifact> TaskArtifacts => Set<TaskArtifact>();
    public DbSet<TaskLlmCallLog> TaskLlmCallLogs => Set<TaskLlmCallLog>();
    /// <summary>Wiki 页面表（V4：直接归属 WikiVersion，不再通过 Wiki 关联）</summary>
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<RepositoryVersion> RepositoryVersions => Set<RepositoryVersion>();
    public DbSet<WikiSpace> WikiSpaces => Set<WikiSpace>();
    public DbSet<WikiVersion> WikiVersions => Set<WikiVersion>();
    public DbSet<WikiPageRelation> WikiPageRelations => Set<WikiPageRelation>();
    public DbSet<CodeEmbeddingChunk> CodeEmbeddingChunks => Set<CodeEmbeddingChunk>();
    public DbSet<WikiEmbeddingChunk> WikiEmbeddingChunks => Set<WikiEmbeddingChunk>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<RepositoryPromptOverride> RepositoryPromptOverrides => Set<RepositoryPromptOverride>();
    public DbSet<PromptTemplateHistory> PromptTemplateHistories => Set<PromptTemplateHistory>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<CodeIndexEntry> CodeIndexEntries => Set<CodeIndexEntry>();
    public DbSet<CodeIndexChunk> CodeIndexChunks => Set<CodeIndexChunk>();
    public DbSet<LlmCallMetric> LlmCallMetrics => Set<LlmCallMetric>();
    public DbSet<ProviderModelMetadataEntity> ProviderModelMetadata => Set<ProviderModelMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryConfiguration());
        modelBuilder.ApplyConfiguration(new TaskRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TaskArtifactConfiguration());
        modelBuilder.ApplyConfiguration(new TaskLlmCallLogConfiguration());
        modelBuilder.ApplyConfiguration(new WikiPageConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WikiSpaceConfiguration());
        modelBuilder.ApplyConfiguration(new WikiVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WikiPageRelationConfiguration());
        modelBuilder.ApplyConfiguration(new CodeEmbeddingChunkConfiguration());
        modelBuilder.ApplyConfiguration(new WikiEmbeddingChunkConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryPromptOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTemplateHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSettingConfiguration());
        modelBuilder.ApplyConfiguration(new CodeIndexEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CodeIndexChunkConfiguration());
        modelBuilder.ApplyConfiguration(new LlmCallMetricConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderModelMetadataConfiguration());
    }
}
