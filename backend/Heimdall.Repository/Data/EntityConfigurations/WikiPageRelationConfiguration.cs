using Heimdall.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Heimdall.Repository.Data.EntityConfigurations;

public class WikiPageRelationConfiguration : IEntityTypeConfiguration<WikiPageRelation>
{
    public void Configure(EntityTypeBuilder<WikiPageRelation> builder)
    {
        builder.ToTable("wiki_page_relations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RelationType).HasColumnName("relation_type").HasMaxLength(32).IsRequired().HasDefaultValue("related_to");
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(e => e.WikiVersion)
            .WithMany()
            .HasForeignKey(e => e.WikiVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SourcePage)
            .WithMany(p => p.SourceRelations)
            .HasForeignKey(e => e.SourcePageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TargetPage)
            .WithMany(p => p.TargetRelations)
            .HasForeignKey(e => e.TargetPageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.WikiVersionId, e.SourcePageId, e.TargetPageId, e.RelationType })
            .HasDatabaseName("ix_wiki_page_relations_version_src_tgt_type")
            .IsUnique();
    }
}
