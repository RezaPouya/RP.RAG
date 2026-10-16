using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class ProductTagDbConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> e)
    {
        e.ToTable("ProductTags");
        e.HasKey(x => new { x.ProductId, x.TagId });

        e.HasOne(x => x.Product)
         .WithMany(p => p.ProductTags)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Tag)
         .WithMany(t => t.ProductTags)
         .HasForeignKey(x => x.TagId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}