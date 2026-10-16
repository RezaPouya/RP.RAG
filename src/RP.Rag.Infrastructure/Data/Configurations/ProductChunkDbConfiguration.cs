using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class ProductChunkDbConfiguration : IEntityTypeConfiguration<ProductChunk>
{
    public void Configure(EntityTypeBuilder<ProductChunk> e)
    {
        e.ToTable("ProductChunks");
        e.HasKey(x => x.Id);

        e.Property(x => x.ChunkType).HasMaxLength(50).IsRequired();
        e.Property(x => x.Header).HasMaxLength(200);
        e.Property(x => x.ChunkText).HasColumnType("text").IsRequired();
        e.Property(x => x.EmbeddingText).HasColumnType("text").IsRequired();
        e.Property(x => x.ChunkTextHash).HasMaxLength(64).IsRequired();

        e.Property(x => x.Embedding).HasColumnType("vector(1024)");

        e.Property(x => x.EmbeddingModel).HasMaxLength(100);
        e.Property(x => x.EmbeddingVersion).HasMaxLength(20);
        e.Property(x => x.EmbeddingStatus).HasMaxLength(20).IsRequired();

        e.HasOne(x => x.Product)
         .WithMany(p => p.Chunks)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Document)
         .WithMany(d => d.Chunks)
         .HasForeignKey(x => x.DocumentId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.EmbeddingModel);

        e.HasIndex(x => x.Embedding)
         .HasMethod("hnsw")
         .HasOperators("vector_cosine_ops")
         .HasStorageParameter("m", 16)
         .HasStorageParameter("ef_construction", 64);
    }
}
