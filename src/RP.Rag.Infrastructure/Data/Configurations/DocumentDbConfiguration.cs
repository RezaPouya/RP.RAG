using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class ProductDocumentDbConfiguration : IEntityTypeConfiguration<ProductDocument>
{
    public void Configure(EntityTypeBuilder<ProductDocument> e)
    {
        e.ToTable("ProductDocuments");
        e.HasKey(x => x.Id);

        e.Property(x => x.Type).HasMaxLength(50).IsRequired();
        e.Property(x => x.Title).HasMaxLength(300).IsRequired();
        e.Property(x => x.Content).HasColumnType("text");

        e.HasOne(x => x.Product)
         .WithMany(p => p.Documents)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.ProductId);
        e.HasIndex(x => x.Type);
    }
}
