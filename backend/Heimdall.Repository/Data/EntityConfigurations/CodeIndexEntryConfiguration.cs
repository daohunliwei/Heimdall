using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public sealed class CodeIndexEntryConfiguration : IEntityTypeConfiguration<CodeIndexEntry>
{
    public void Configure(EntityTypeBuilder<CodeIndexEntry> builder)
    {
        builder.ToTable("code_index_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.ModuleName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.FileType).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Language).HasMaxLength(64);
        builder.Property(e => e.ImportanceScore).HasDefaultValue(0);
        builder.Property(e => e.ExportedSymbolsJson).HasColumnName("exported_symbols").HasColumnType("text");
        builder.Property(e => e.DependencyHintsJson).HasColumnName("dependency_hints").HasColumnType("text");

        builder.HasIndex(e => e.RepositoryVersionId);
        builder.HasIndex(e => new { e.RepositoryVersionId, e.FilePath }).IsUnique();
        builder.HasIndex(e => e.ModuleName);
    }
}

public sealed class CodeIndexChunkConfiguration : IEntityTypeConfiguration<CodeIndexChunk>
{
    public void Configure(EntityTypeBuilder<CodeIndexChunk> builder)
    {
        builder.ToTable("code_index_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content).IsRequired().HasColumnType("text");
        builder.Property(c => c.StartLine).IsRequired();
        builder.Property(c => c.EndLine).IsRequired();
        builder.Property(c => c.Language).HasMaxLength(64);

        builder.HasIndex(c => c.CodeIndexEntryId);

        builder.HasOne(c => c.CodeIndexEntry)
            .WithMany(e => e.Chunks)
            .HasForeignKey(c => c.CodeIndexEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
