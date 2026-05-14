using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiPageConfiguration : IEntityTypeConfiguration<WikiPage>
{
    public void Configure(EntityTypeBuilder<WikiPage> builder)
    {
        builder.ToTable("wiki_pages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PageOrder).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Title).HasColumnType("text").IsRequired();
        builder.Property(e => e.ContentMarkdown).HasColumnType("text");
        builder.Property(e => e.NavTitle).HasColumnName("nav_title").HasMaxLength(256);
        builder.Property(e => e.PageType).HasColumnName("page_type").HasMaxLength(16).HasDefaultValue("article");
        builder.Property(e => e.Importance).HasMaxLength(8).HasDefaultValue("medium");
        builder.Property(e => e.Depth).HasColumnName("depth").HasDefaultValue(0);
        builder.Property(e => e.OutlineJson).HasColumnName("outline_json").HasColumnType("jsonb");
        builder.Property(e => e.Summary).HasColumnName("summary").HasColumnType("text");
        builder.Property(e => e.SourceCoverageJson).HasColumnName("source_coverage_json").HasColumnType("jsonb");
        builder.Property(e => e.FilePaths).HasColumnType("text[]");
        builder.Property(e => e.TokenCount).HasColumnName("token_count");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).HasDefaultValue("ready");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasOne(e => e.Wiki).WithMany(w => w.Pages).HasForeignKey(e => e.WikiId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.WikiVersion).WithMany(v => v.WikiPages).HasForeignKey(e => e.WikiVersionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Task).WithMany(t => t.WikiPages).HasForeignKey(e => e.TaskId);
        builder.HasOne(e => e.ParentPage).WithMany(p => p.Children).HasForeignKey(e => e.ParentPageId);
    }
}
