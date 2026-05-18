using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class RepositoryConfiguration : IEntityTypeConfiguration<Core.Entities.Repository>
{
    public void Configure(EntityTypeBuilder<Core.Entities.Repository> builder)
    {
        builder.ToTable("repositories");
        builder.HasKey(e => e.Id);

        // V2 新增字段（显式指定列名以匹配数据库 snake_case 列）
        builder.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(32).IsRequired().HasDefaultValue("github");
        builder.Property(e => e.ProviderRepositoryKey).HasColumnName("provider_repository_key").HasMaxLength(256);
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(512).IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);

        // 原有字段（保持兼容，不指定列名延续 Npgsql 默认行为）
        builder.Property(e => e.Owner).HasMaxLength(128).IsRequired();
        builder.Property(e => e.RepoName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.RepoType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.RepoUrl).HasColumnType("text");
        builder.Property(e => e.CloneUrl).HasColumnType("text");
        builder.Property(e => e.DefaultBranch).HasMaxLength(128).HasDefaultValue("main");
        builder.Property(e => e.DefaultLanguage).HasMaxLength(8).HasDefaultValue("zh");
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => new { e.Owner, e.RepoName, e.RepoType }).IsUnique();
        builder.HasIndex(e => new { e.ProviderType, e.ProviderRepositoryKey }).IsUnique()
            .HasFilter("provider_repository_key IS NOT NULL");
    }
}
