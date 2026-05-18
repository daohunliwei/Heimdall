using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

/// <summary>
/// 任务工件实体映射。
/// 该映射确保不同阶段工件可以按任务、类型与工件键稳定查询和幂等更新。
/// </summary>
public class TaskArtifactConfiguration : IEntityTypeConfiguration<TaskArtifact>
{
    /// <summary>
    /// 配置任务工件表结构、字段类型与索引。
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskArtifact> builder)
    {
        builder.ToTable("task_artifacts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ArtifactType)
            .HasColumnName("artifact_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ArtifactKey)
            .HasColumnName("artifact_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.StageName)
            .HasColumnName("stage_name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("completed");

        builder.Property(e => e.Sequence)
            .HasColumnName("sequence")
            .HasDefaultValue(0);

        builder.Property(e => e.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(64);

        builder.Property(e => e.Summary)
            .HasColumnName("summary")
            .HasColumnType("text");

        builder.Property(e => e.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(e => e.Task)
            .WithMany(t => t.Artifacts)
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TaskId, e.ArtifactType, e.ArtifactKey })
            .HasDatabaseName("ix_task_artifacts_task_type_key")
            .IsUnique();

        builder.HasIndex(e => new { e.TaskId, e.StageName, e.Sequence })
            .HasDatabaseName("ix_task_artifacts_task_stage_sequence");
    }
}
