using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class EmbeddingDocumentConfiguration : IEntityTypeConfiguration<EmbeddingDocument>
{
    public void Configure(EntityTypeBuilder<EmbeddingDocument> builder)
    {
        builder.ToTable("embedding_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FilePath).HasColumnType("text").IsRequired();
        builder.Property(e => e.ChunkIndex).IsRequired();
        builder.Property(e => e.TextContent).HasColumnType("text").IsRequired();
        builder.Property(e => e.Embedding).HasColumnType("bytea");
        builder.Property(e => e.IsCode).HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasOne(e => e.Repository).WithMany(r => r.EmbeddingDocuments).HasForeignKey(e => e.RepositoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
