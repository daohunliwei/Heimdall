using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiSpaceConfiguration : IEntityTypeConfiguration<WikiSpace>
{
    public void Configure(EntityTypeBuilder<WikiSpace> builder)
    {
        builder.ToTable("wiki_spaces");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Language).HasColumnName("language").HasMaxLength(8).HasDefaultValue("zh");
        builder.Property(e => e.ViewType).HasColumnName("view_type").HasMaxLength(32).HasDefaultValue("default");
        builder.Property(e => e.Title).HasColumnName("title").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(e => e.PublishedWikiVersionId).HasColumnName("published_wiki_version_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(e => e.Repository)
            .WithMany(r => r.WikiSpaces)
            .HasForeignKey(e => e.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // 同一仓库、同一语言、同一视角唯一
        builder.HasIndex(e => new { e.RepositoryId, e.Language, e.ViewType })
            .HasDatabaseName("ix_wiki_spaces_repo_lang_view")
            .IsUnique();
    }
}
