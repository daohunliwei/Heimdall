using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class CodeEmbeddingChunkConfiguration : IEntityTypeConfiguration<CodeEmbeddingChunk>
{
    public void Configure(EntityTypeBuilder<CodeEmbeddingChunk> builder)
    {
        builder.ToTable("code_embedding_chunks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(e => e.SymbolPath).HasColumnName("symbol_path");
        builder.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
        builder.Property(e => e.ChunkType).HasColumnName("chunk_type").HasMaxLength(32).HasDefaultValue("code_block");
        builder.Property(e => e.Language).HasColumnName("language").HasMaxLength(32);
        builder.Property(e => e.StartLine).HasColumnName("start_line");
        builder.Property(e => e.EndLine).HasColumnName("end_line");
        builder.Property(e => e.ContentRaw).HasColumnName("content_raw").HasColumnType("text").IsRequired();
        builder.Property(e => e.ContentNormalized).HasColumnName("content_normalized").HasColumnType("text");
        builder.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(e => e.TokenCount).HasColumnName("token_count");
        builder.Property(e => e.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(64);
        builder.Property(e => e.EmbeddingVector).HasColumnName("embedding_vector").HasColumnType("bytea");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(e => e.RepositoryVersion)
            .WithMany()
            .HasForeignKey(e => e.RepositoryVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.RepositoryVersionId, e.FilePath, e.ChunkIndex })
            .HasDatabaseName("ix_code_embedding_chunks_version_file_chunk")
            .IsUnique();
    }
}
