using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class LlmCallMetricConfiguration : IEntityTypeConfiguration<LlmCallMetric>
{
    public void Configure(EntityTypeBuilder<LlmCallMetric> builder)
    {
        builder.ToTable("llm_call_metrics");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Stage).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Model).HasMaxLength(64).IsRequired();
        builder.Property(e => e.InputTokens).HasDefaultValue(0);
        builder.Property(e => e.OutputTokens).HasDefaultValue(0);
        builder.Property(e => e.CacheHitTokens).HasDefaultValue(0);
        builder.Property(e => e.LatencyMs).HasDefaultValue(0);
        builder.Property(e => e.Success).HasDefaultValue(true);
        builder.Property(e => e.ErrorType).HasMaxLength(64);
        builder.Property(e => e.IsEstimated).HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Task)
            .WithMany()
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TaskId).HasDatabaseName("idx_llm_call_metrics_task");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_llm_call_metrics_created");
        builder.HasIndex(e => new { e.Provider, e.Model }).HasDatabaseName("idx_llm_call_metrics_provider_model");
    }
}
