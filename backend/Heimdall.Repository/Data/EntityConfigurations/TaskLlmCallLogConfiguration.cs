using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class TaskLlmCallLogConfiguration : IEntityTypeConfiguration<TaskLlmCallLog>
{
    public void Configure(EntityTypeBuilder<TaskLlmCallLog> builder)
    {
        builder.ToTable("task_llm_call_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StepOrder).IsRequired();
        builder.Property(e => e.CallType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(32);
        builder.Property(e => e.Model).HasMaxLength(64);
        builder.Property(e => e.PromptTokens).HasDefaultValue(0);
        builder.Property(e => e.CompletionTokens).HasDefaultValue(0);
        builder.Property(e => e.TotalTokens).HasDefaultValue(0);
        builder.Property(e => e.RequestPreview).HasColumnType("text");
        builder.Property(e => e.ResponsePreview).HasColumnType("text");
        builder.Property(e => e.LatencyMs);
        builder.Property(e => e.IsError).HasDefaultValue(false);
        builder.Property(e => e.ErrorMessage).HasColumnType("text");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasOne(e => e.Task).WithMany(t => t.LlmCallLogs).HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.TaskId, e.StepOrder }).HasDatabaseName("idx_task_llm_call_logs_task");
    }
}
