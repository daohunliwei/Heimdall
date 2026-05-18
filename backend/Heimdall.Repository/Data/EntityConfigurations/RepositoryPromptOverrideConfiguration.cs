using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class RepositoryPromptOverrideConfiguration : IEntityTypeConfiguration<RepositoryPromptOverride>
{
    public void Configure(EntityTypeBuilder<RepositoryPromptOverride> builder)
    {
        builder.ToTable("repository_prompt_overrides");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OverrideContent).HasColumnType("text");
        builder.Property(e => e.Strategy).HasMaxLength(16).IsRequired().HasDefaultValue("override");
        builder.Property(e => e.Priority).HasDefaultValue(0);
        builder.Property(e => e.IsEnabled).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasOne(e => e.Repository).WithMany().HasForeignKey(e => e.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.PromptTemplate).WithMany(p => p.RepositoryOverrides).HasForeignKey(e => e.PromptTemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RepositoryId, e.PromptTemplateId }).IsUnique();
    }
}
