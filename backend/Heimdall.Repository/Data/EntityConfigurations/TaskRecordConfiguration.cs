using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class TaskRecordConfiguration : IEntityTypeConfiguration<TaskRecord>
{
    public void Configure(EntityTypeBuilder<TaskRecord> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TaskType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        builder.Property(e => e.SourceBranch).HasColumnName("source_branch").HasMaxLength(128).IsRequired().HasDefaultValue("main");
        builder.Property(e => e.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(32);
        builder.Property(e => e.Model).HasMaxLength(64);
        builder.Property(e => e.Language).HasMaxLength(8);
        builder.Property(e => e.ProgressPercent).HasDefaultValue(0);
        builder.Property(e => e.ProgressMessage).HasColumnType("text");
        builder.Property(e => e.TotalPromptTokens).HasDefaultValue(0);
        builder.Property(e => e.TotalCompletionTokens).HasDefaultValue(0);
        builder.Property(e => e.ResultJson).HasColumnType("jsonb");
        builder.Property(e => e.ErrorMessage).HasColumnType("text");
        builder.Property(e => e.CurrentStage).HasColumnName("current_stage").HasMaxLength(64).IsRequired().HasDefaultValue("queued");
        builder.Property(e => e.CurrentStageStatus).HasColumnName("current_stage_status").HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        builder.Property(e => e.LastSuccessfulStage).HasColumnName("last_successful_stage").HasMaxLength(64);
        builder.Property(e => e.LastArtifactId).HasColumnName("last_artifact_id");
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        // V2 版本关联字段
        builder.Property(e => e.TargetBranch).HasColumnName("target_branch").HasMaxLength(128);
        builder.Property(e => e.ResolvedRepositoryVersionId).HasColumnName("resolved_repository_version_id");
        builder.Property(e => e.ResultWikiVersionId).HasColumnName("result_wiki_version_id");
        builder.Property(e => e.RefreshStrategy).HasColumnName("refresh_strategy").HasMaxLength(16);
        builder.Property(e => e.ForceRefresh).HasColumnName("force_refresh").HasDefaultValue(false);
        builder.Property(e => e.ConfigHash).HasColumnName("config_hash").HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasOne(e => e.Repository).WithMany(r => r.Tasks).HasForeignKey(e => e.RepositoryId);
        builder.HasOne<TaskArtifact>().WithMany().HasForeignKey(e => e.LastArtifactId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.ResolvedRepositoryVersion).WithMany().HasForeignKey(e => e.ResolvedRepositoryVersionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.ResultWikiVersion).WithMany().HasForeignKey(e => e.ResultWikiVersionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.User).WithMany(u => u.Tasks).HasForeignKey(e => e.UserId);

        // 并发控制：同一仓库+分支，同一时间仅允许一个 running 任务
        builder.HasIndex(e => new { e.RepositoryId, e.SourceBranch })
            .HasFilter("status = 'running'")
            .IsUnique()
            .HasDatabaseName("idx_one_running_task_per_repo_branch");

        // 去重：同一仓库+分支+任务类型，同一时间仅允许一个 pending 任务
        builder.HasIndex(e => new { e.RepositoryId, e.SourceBranch, e.TaskType })
            .HasFilter("status = 'pending'")
            .IsUnique()
            .HasDatabaseName("idx_one_pending_task_per_repo_branch_type");
    }
}
