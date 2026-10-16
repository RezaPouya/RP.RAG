using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class ProductSyncDbConfiguration : IEntityTypeConfiguration<ProductSync>
{
    public void Configure(EntityTypeBuilder<ProductSync> e)
    {
        e.ToTable("ProductSyncs");
        e.HasKey(x => x.ProductId);

        e.Property(x => x.ContentChanged).HasDefaultValue(true);
        e.Property(x => x.EmbeddingChanged).HasDefaultValue(true);

        e.HasOne(x => x.Product)
            .WithOne(p => p.Sync)
            .HasForeignKey<ProductSync>(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}