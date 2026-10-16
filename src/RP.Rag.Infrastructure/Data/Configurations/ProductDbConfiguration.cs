using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class ProductDbConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("Products");
        e.HasKey(x => x.Id);

        e.Property(x => x.Name).HasMaxLength(300).IsRequired();
        e.Property(x => x.Summary).HasMaxLength(1000);
        e.Property(x => x.SKU).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasColumnType("text");
        e.Property(x => x.Price).HasPrecision(18, 2);

        e.HasOne(x => x.Category)
         .WithMany(c => c.Products)
         .HasForeignKey(x => x.ProductCategoryId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.SKU).IsUnique();
        e.HasIndex(x => x.IsActive);
        e.HasIndex(x => x.ProductCategoryId);

        e.HasOne(x => x.Sync)
         .WithOne(s => s.Product)
         .HasForeignKey<ProductSync>(s => s.ProductId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
