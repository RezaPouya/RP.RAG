using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities;

namespace RP.Rag.Infrastructure.Data.Configurations
{
    internal class ProductCategoryDbConfiguration : IEntityTypeConfiguration<ProductCategory>
    {
        public void Configure(EntityTypeBuilder<ProductCategory> b)
        {
            b.ToTable("ProductCategories");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description);

            b.HasIndex(x => x.Name);
        }
    }
}
