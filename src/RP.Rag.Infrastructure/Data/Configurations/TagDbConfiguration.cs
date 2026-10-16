using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal class TagDbConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> e)
    {
        e.ToTable("Tags");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
    }
}
