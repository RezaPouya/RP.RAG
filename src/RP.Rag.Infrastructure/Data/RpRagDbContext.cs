using Microsoft.EntityFrameworkCore;
using RP.Rag.Domain.Entities;
using RP.Rag.Domain.Entities.Products;
using ProductDocument = RP.Rag.Domain.Entities.Products.ProductDocument;

namespace RP.Rag.Infrastructure.Data;

public sealed class RpRagDbContext(DbContextOptions<RpRagDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductChunk> ProductChunks => Set<ProductChunk>();
    public DbSet<ProductSync> ProductSyncs => Set<ProductSync>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProductDocument> Documents => Set<ProductDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpRagDbContext).Assembly);
    }
}