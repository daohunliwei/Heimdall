using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class PromptTemplateHistoryConfiguration : IEntityTypeConfiguration<PromptTemplateHistory>
{
    public void Configure(EntityTypeBuilder<PromptTemplateHistory> builder)
    {
        builder.ToTable("prompt_template_history");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.TemplateContent).HasColumnType("text").IsRequired();
        builder.Property(e => e.ChangedAt).IsRequired();
        builder.HasOne(e => e.PromptTemplate)
            .WithMany(p => p.Versions)
            .HasForeignKey(e => e.PromptTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.PromptTemplateId, e.Version }).IsUnique();
    }
}
