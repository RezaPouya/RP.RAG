using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RP.Rag.Domain.Entities;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using System.Text.Json;

namespace RP.Rag.Infrastructure.SeedData;

public sealed class PersianDataSeedLoader(RpRagDbContext db, ILogger<PersianDataSeedLoader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SeedResult> LoadAsync(string dataRoot, bool clearExisting = false, CancellationToken ct = default)
    {
        var seedDir = Path.Combine(dataRoot, "seed");

        if (!Directory.Exists(seedDir))
            throw new DirectoryNotFoundException($"Seed directory not found: {seedDir}");

        if (clearExisting)
            await ClearAsync(ct);

        var categories = await ReadAsync<List<CategorySeed>>(Path.Combine(seedDir, "categories_fa.json"), ct);
        var tags = await ReadAsync<List<TagSeed>>(Path.Combine(seedDir, "tags_fa.json"), ct);
        var products = await ReadAsync<List<ProductSeed>>(Path.Combine(seedDir, "products_fa.json"), ct);

        logger.LogInformation("Persian seed files loaded. Categories={C}, Tags={T}, Products={P}",
            categories.Count, tags.Count, products.Count);

        await UpsertCategoriesAsync(categories, ct);
        await UpsertTagsAsync(tags, ct);
        await UpsertProductsAsync(products, ct);

        var result = new SeedResult
        {
            Categories = categories.Count,
            Tags = tags.Count,
            Products = products.Count,
            Documents = products.Sum(p => p.Documents.Count)
        };

        logger.LogInformation(
            "Persian seed complete. {Categories} categories, {Tags} tags, " +
            "{Products} products, {Documents} documents.",
            result.Categories, result.Tags, result.Products, result.Documents);

        return result;
    }

    private async Task ClearAsync(CancellationToken ct)
    {
        logger.LogWarning("Clearing existing seed data...");

        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"ProductChunks\", \"ProductDocuments\", \"ProductTags\", " +
            "\"ProductSyncs\", \"Products\", \"Tags\", \"ProductCategories\" " +
            "RESTART IDENTITY CASCADE;",
            ct);
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Seed file not found: {path}");

        await using var stream = File.OpenRead(path);

        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);

        return result ?? throw new InvalidOperationException($"Failed to deserialize {path}");
    }

    private async Task UpsertCategoriesAsync(List<CategorySeed> seeds, CancellationToken ct)
    {
        var existing = await db.Categories.ToDictionaryAsync(c => c.Id, ct);

        foreach (var s in seeds)
        {
            if (existing.TryGetValue(s.Id, out var e))
            {
                e.Name = s.Name;
                e.Description = s.Description;
            }
            else
            {
                db.Categories.Add(new ProductCategory
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description
                });
            }
        }

        await db.SaveChangesAsync(ct);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"ProductCategories\"','Id'), " +
            "(SELECT COALESCE(MAX(\"Id\"), 1) FROM \"ProductCategories\"));",
            ct);
    }

    private async Task UpsertTagsAsync(List<TagSeed> seeds, CancellationToken ct)
    {
        var existing = await db.Tags.ToDictionaryAsync(t => t.Id, ct);

        foreach (var s in seeds)
        {
            if (existing.TryGetValue(s.Id, out var e))
            {
                e.Name = s.Name;
            }
            else
            {
                db.Tags.Add(new Tag { Id = s.Id, Name = s.Name });
            }
        }

        await db.SaveChangesAsync(ct);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"Tags\"','Id'), " +
            "(SELECT COALESCE(MAX(\"Id\"), 1) FROM \"Tags\"));",
            ct);
    }

    private async Task UpsertProductsAsync(List<ProductSeed> seeds, CancellationToken ct)
    {
        var existingProducts = await db.Products
            .Include(p => p.ProductTags)
            .Include(p => p.Documents)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var s in seeds)
        {
            if (existingProducts.TryGetValue(s.Id, out var e))
            {
                e.Name = s.Name;
                e.Summary = s.Summary;
                e.SKU = s.Sku;
                e.Description = s.Description;
                e.IsActive = s.IsActive;
                e.Price = s.Price;
                e.StockCount = s.StockCount;
                e.ProductCategoryId = s.ProductCategoryId;
                e.UpdatedAt = DateTime.UtcNow;

                SyncTags(e, s.TagIds);
                SyncDocuments(e, s.Documents);
            }
            else
            {
                var p = new Product
                {
                    Id = s.Id,
                    Name = s.Name,
                    Summary = s.Summary,
                    SKU = s.Sku,
                    Description = s.Description,
                    IsActive = s.IsActive,
                    Price = s.Price,
                    StockCount = s.StockCount,
                    ProductCategoryId = s.ProductCategoryId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                SyncTags(p, s.TagIds);
                SyncDocuments(p, s.Documents);

                db.Products.Add(p);
            }
        }

        await db.SaveChangesAsync(ct);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"Products\"','Id'), " +
            "(SELECT COALESCE(MAX(\"Id\"), 1) FROM \"Products\"));",
            ct);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"ProductDocuments\"','Id'), " +
            "(SELECT COALESCE(MAX(\"Id\"), 1) FROM \"ProductDocuments\"));",
            ct);
    }

    private static void SyncTags(Product p, List<int> tagIds)
    {
        var desired = tagIds.ToHashSet();
        var current = p.ProductTags.Select(pt => pt.TagId).ToHashSet();

        foreach (var toRemove in current.Except(desired).ToList())
        {
            var pt = p.ProductTags.First(x => x.TagId == toRemove);
            p.ProductTags.Remove(pt);
        }

        foreach (var toAdd in desired.Except(current))
        {
            p.ProductTags.Add(new ProductTag
            {
                ProductId = p.Id,
                TagId = toAdd
            });
        }
    }

    private static void SyncDocuments(Product p, List<DocumentSeed> docs)
    {
        p.Documents.Clear();
        foreach (var d in docs)
        {
            p.Documents.Add(new ProductDocument
            {
                ProductId = p.Id,
                Type = d.Type,
                Title = d.Title,
                Content = d.Content,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}