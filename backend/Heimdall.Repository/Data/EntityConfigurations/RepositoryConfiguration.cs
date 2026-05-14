using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class RepositoryConfiguration : IEntityTypeConfiguration<Core.Entities.Repository>
{
    public void Configure(EntityTypeBuilder<Core.Entities.Repository> builder)
    {
        builder.ToTable("repositories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Owner).HasMaxLength(128).IsRequired();
        builder.Property(e => e.RepoName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.RepoType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.RepoUrl).HasColumnType("text");
        builder.Property(e => e.CloneUrl).HasColumnType("text");
        builder.Property(e => e.DefaultBranch).HasMaxLength(128).HasDefaultValue("main");
        builder.Property(e => e.DefaultLanguage).HasMaxLength(8).HasDefaultValue("zh");
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => new { e.Owner, e.RepoName, e.RepoType }).IsUnique();
    }
}
