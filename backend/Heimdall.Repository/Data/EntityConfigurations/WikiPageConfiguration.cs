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
        builder.Property(e => e.Importance).HasMaxLength(8).HasDefaultValue("medium");
        builder.Property(e => e.FilePaths).HasColumnType("text[]");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasOne(e => e.Wiki).WithMany(w => w.Pages).HasForeignKey(e => e.WikiId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Task).WithMany(t => t.WikiPages).HasForeignKey(e => e.TaskId);
        builder.HasOne(e => e.ParentPage).WithMany(p => p.Children).HasForeignKey(e => e.ParentPageId);
    }
}
