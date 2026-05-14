using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(128).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
        builder.Property(e => e.Value).HasColumnType("text").IsRequired();
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.UpdatedAt).IsRequired();
    }
}
