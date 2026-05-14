using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiVersionConfiguration : IEntityTypeConfiguration<WikiVersion>
{
    public void Configure(EntityTypeBuilder<WikiVersion> builder)
    {
        builder.ToTable("wiki_versions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.VersionNo).HasColumnName("version_no").HasDefaultValue(1);
        builder.Property(e => e.GenerationMode).HasColumnName("generation_mode").HasMaxLength(16).HasDefaultValue("latest");
        builder.Property(e => e.GenerationProfile).HasColumnName("generation_profile").HasMaxLength(32).HasDefaultValue("comprehensive");
        builder.Property(e => e.PromptProfileHash).HasColumnName("prompt_profile_hash").HasMaxLength(64);
        builder.Property(e => e.ModelProfileHash).HasColumnName("model_profile_hash").HasMaxLength(64);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).HasDefaultValue("draft");
        builder.Property(e => e.IsForceRefresh).HasColumnName("is_force_refresh").HasDefaultValue(false);
        builder.Property(e => e.PageCount).HasColumnName("page_count");
        builder.Property(e => e.TocDepth).HasColumnName("toc_depth");
        builder.Property(e => e.SummaryMarkdown).HasColumnName("summary_markdown").HasColumnType("text");
        builder.Property(e => e.StructureJson).HasColumnName("structure_json").HasColumnType("text");
        builder.Property(e => e.CreatedByTaskId).HasColumnName("created_by_task_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        builder.HasOne(e => e.WikiSpace)
            .WithMany(s => s.WikiVersions)
            .HasForeignKey(e => e.WikiSpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.RepositoryVersion)
            .WithMany(v => v.WikiVersions)
            .HasForeignKey(e => e.RepositoryVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // 查询索引
        builder.HasIndex(e => new { e.WikiSpaceId, e.VersionNo })
            .HasDatabaseName("ix_wiki_versions_space_version")
            .IsUnique();

        builder.HasIndex(e => e.RepositoryVersionId)
            .HasDatabaseName("ix_wiki_versions_repo_version");
    }
}
