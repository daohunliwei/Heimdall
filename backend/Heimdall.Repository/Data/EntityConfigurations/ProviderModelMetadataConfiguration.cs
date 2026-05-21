using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class ProviderModelMetadataConfiguration : IEntityTypeConfiguration<ProviderModelMetadataEntity>
{
    public void Configure(EntityTypeBuilder<ProviderModelMetadataEntity> builder)
    {
        builder.ToTable("provider_model_metadata");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ProviderKey, e.ModelName }).IsUnique();
        builder.Property(e => e.ProviderKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ModelName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.BillingType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.MaxContextTokens).IsRequired();
        builder.Property(e => e.MaxOutputTokens).IsRequired();
        builder.Property(e => e.InputTokenPrice).HasColumnType("decimal(10,6)");
        builder.Property(e => e.OutputTokenPrice).HasColumnType("decimal(10,6)");
        builder.Property(e => e.CallPrice).HasColumnType("decimal(10,6)");
        builder.Property(e => e.ContextFillRatio).IsRequired();
        builder.Property(e => e.ContextWarningThreshold).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }
}
