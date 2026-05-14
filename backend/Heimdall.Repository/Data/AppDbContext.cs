using Heimdall.Core.Entities;
using Heimdall.Repository.Data.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Core.Entities.Repository> Repositories => Set<Core.Entities.Repository>();
    public DbSet<TaskRecord> Tasks => Set<TaskRecord>();
    public DbSet<TaskLlmCallLog> TaskLlmCallLogs => Set<TaskLlmCallLog>();
    public DbSet<Wiki> Wikis => Set<Wiki>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<RepositoryVersion> RepositoryVersions => Set<RepositoryVersion>();
    public DbSet<WikiSpace> WikiSpaces => Set<WikiSpace>();
    public DbSet<WikiVersion> WikiVersions => Set<WikiVersion>();
    public DbSet<WikiPageRelation> WikiPageRelations => Set<WikiPageRelation>();
    public DbSet<CodeEmbeddingChunk> CodeEmbeddingChunks => Set<CodeEmbeddingChunk>();
    public DbSet<WikiEmbeddingChunk> WikiEmbeddingChunks => Set<WikiEmbeddingChunk>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<RepositoryPromptOverride> RepositoryPromptOverrides => Set<RepositoryPromptOverride>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryConfiguration());
        modelBuilder.ApplyConfiguration(new TaskRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TaskLlmCallLogConfiguration());
        modelBuilder.ApplyConfiguration(new WikiConfiguration());
        modelBuilder.ApplyConfiguration(new WikiPageConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WikiSpaceConfiguration());
        modelBuilder.ApplyConfiguration(new WikiVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WikiPageRelationConfiguration());
        modelBuilder.ApplyConfiguration(new CodeEmbeddingChunkConfiguration());
        modelBuilder.ApplyConfiguration(new WikiEmbeddingChunkConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryPromptOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new SystemSettingConfiguration());
    }
}
