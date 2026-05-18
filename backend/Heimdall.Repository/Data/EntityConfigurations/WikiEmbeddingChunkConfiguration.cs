using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiEmbeddingChunkConfiguration : IEntityTypeConfiguration<WikiEmbeddingChunk>
{
    public void Configure(EntityTypeBuilder<WikiEmbeddingChunk> builder)
    {
        builder.ToTable("wiki_embedding_chunks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
        builder.Property(e => e.ChunkType).HasColumnName("chunk_type").HasMaxLength(32).HasDefaultValue("section");
        builder.Property(e => e.ContentRaw).HasColumnName("content_raw").HasColumnType("text").IsRequired();
        builder.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(e => e.TokenCount).HasColumnName("token_count");
        builder.Property(e => e.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(64);
        builder.Property(e => e.EmbeddingVector).HasColumnName("embedding_vector").HasColumnType("bytea");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(e => e.WikiVersion)
            .WithMany()
            .HasForeignKey(e => e.WikiVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.WikiPage)
            .WithMany()
            .HasForeignKey(e => e.WikiPageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.WikiVersionId, e.WikiPageId, e.ChunkIndex })
            .HasDatabaseName("ix_wiki_embedding_chunks_version_page_chunk")
            .IsUnique();
    }
}
