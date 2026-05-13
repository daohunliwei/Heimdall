using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.ToTable("prompt_templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Layer).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ScopeType).HasMaxLength(16).IsRequired().HasDefaultValue("global");
        builder.Property(e => e.ScopeValue).HasMaxLength(128);
        builder.Property(e => e.TemplateContent).HasColumnType("text").IsRequired();
        builder.Property(e => e.Variables).HasColumnType("text[]");
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => new { e.Name, e.ScopeType, e.ScopeValue }).IsUnique();
    }
}
