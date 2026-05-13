using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiConfiguration : IEntityTypeConfiguration<Wiki>
{
    public void Configure(EntityTypeBuilder<Wiki> builder)
    {
        builder.ToTable("wikis");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasColumnType("text").IsRequired();
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.SourceBranch).HasMaxLength(128).IsRequired().HasDefaultValue("main");
        builder.Property(e => e.Language).HasMaxLength(8).HasDefaultValue("zh");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasOne(e => e.SourceRepository).WithMany(r => r.Wikis).HasForeignKey(e => e.SourceRepositoryId);
        builder.HasIndex(e => new { e.SourceRepositoryId, e.SourceBranch, e.Language }).IsUnique();
    }
}
