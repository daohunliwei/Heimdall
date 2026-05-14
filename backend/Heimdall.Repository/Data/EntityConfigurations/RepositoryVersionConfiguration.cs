using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class RepositoryVersionConfiguration : IEntityTypeConfiguration<RepositoryVersion>
{
    public void Configure(EntityTypeBuilder<RepositoryVersion> builder)
    {
        builder.ToTable("repository_versions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BranchName).HasColumnName("branch_name").HasMaxLength(256).IsRequired();
        builder.Property(e => e.CommitSha).HasColumnName("commit_sha").HasMaxLength(64).IsRequired();
        builder.Property(e => e.TreeFingerprint).HasColumnName("tree_fingerprint").HasMaxLength(128);
        builder.Property(e => e.CommitTime).HasColumnName("commit_time").IsRequired();
        builder.Property(e => e.CommitAuthor).HasColumnName("commit_author").HasMaxLength(256);
        builder.Property(e => e.CommitMessage).HasColumnName("commit_message").HasColumnType("text");
        builder.Property(e => e.SourceStatus).HasColumnName("source_status").HasMaxLength(32).HasDefaultValue("active");
        builder.Property(e => e.IsLatestOnBranch).HasColumnName("is_latest_on_branch").HasDefaultValue(false);
        builder.Property(e => e.VersionSourceConfidence).HasColumnName("version_source_confidence").HasMaxLength(16).HasDefaultValue("exact");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(e => e.Repository)
            .WithMany(r => r.RepositoryVersions)
            .HasForeignKey(e => e.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // 唯一索引：同一仓库、同一分支、同一提交不可重复
        builder.HasIndex(e => new { e.RepositoryId, e.BranchName, e.CommitSha })
            .HasDatabaseName("ix_repository_versions_repo_branch_commit")
            .IsUnique();

        // 查询索引：按仓库 + 分支查找最新版本
        builder.HasIndex(e => new { e.RepositoryId, e.BranchName, e.IsLatestOnBranch })
            .HasDatabaseName("ix_repository_versions_repo_branch_latest");
    }
}
