using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RP.Rag.Domain.Entities;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using System.Text.Json;

namespace RP.Rag.Infrastructure.SeedData;

public sealed class SeedLoader(RpRagDbContext db, ILogger<SeedLoader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SeedResult> LoadAsync(
        string dataRoot,
        string locale = "",
        bool clearExisting = false,
        CancellationToken ct = default)
    {
        var seedDir = Path.Combine(dataRoot, "seed");

        if (!Directory.Exists(seedDir))
            throw new DirectoryNotFoundException($"Seed directory not found: {seedDir}");

        if (clearExisting)
            await ClearAsync(ct);

        var suffix = string.IsNullOrWhiteSpace(locale) ? "" : $"_{locale}";

        var categories = await ReadAsync<List<CategorySeed>>(
            Path.Combine(seedDir, $"categories{suffix}.json"), ct);

        var tags = await ReadAsync<List<TagSeed>>(
            Path.Combine(seedDir, $"tags{suffix}.json"), ct);

        var products = await ReadAsync<List<ProductSeed>>(
            Path.Combine(seedDir, $"products{suffix}.json"), ct);

        logger.LogInformation(
            "Seed files loaded (locale={Locale}). Categories={C}, Tags={T}, Products={P}",
            string.IsNullOrEmpty(locale) ? "(default)" : locale,
            categories.Count, tags.Count, products.Count);

        var insertedCategories = 0;
        var insertedTags = 0;
        var insertedProducts = 0;
        var insertedDocuments = 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            insertedCategories = await InsertCategoriesAsync(categories, ct);
            insertedTags = await InsertTagsAsync(tags, ct);

            var (p, d) = await InsertProductsAsync(products, ct);
            insertedProducts = p;
            insertedDocuments = d;

            await SyncSequencesAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        var result = new SeedResult
        {
            Categories = insertedCategories,
            Tags = insertedTags,
            Products = insertedProducts,
            Documents = insertedDocuments
        };

        logger.LogInformation(
            "Seed complete (insert-only). Inserted {Categories} categories, " +
            "{Tags} tags, {Products} products, {Documents} documents.",
            result.Categories, result.Tags, result.Products, result.Documents);

        return result;
    }

    private async Task ClearAsync(CancellationToken ct)
    {
        logger.LogWarning("Clearing existing seed data...");

        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                "ProductChunks",
                "ProductDocuments",
                "ProductTags",
                "ProductSyncs",
                "Products",
                "Tags",
                "ProductCategories"
            RESTART IDENTITY CASCADE;
            """,
            ct);
    }

    private async Task SyncSequencesAsync(CancellationToken ct)
    {
        const string sql =
            """
            SELECT setval(pg_get_serial_sequence('"ProductCategories"','Id'),
                          COALESCE((SELECT MAX("Id") FROM "ProductCategories"), 0) + 1,
                          false);
            SELECT setval(pg_get_serial_sequence('"Tags"','Id'),
                          COALESCE((SELECT MAX("Id") FROM "Tags"), 0) + 1,
                          false);
            SELECT setval(pg_get_serial_sequence('"Products"','Id'),
                          COALESCE((SELECT MAX("Id") FROM "Products"), 0) + 1,
                          false);
            SELECT setval(pg_get_serial_sequence('"ProductDocuments"','Id'),
                          COALESCE((SELECT MAX("Id") FROM "ProductDocuments"), 0) + 1,
                          false);
            SELECT setval(pg_get_serial_sequence('"ProductChunks"','Id'),
                          COALESCE((SELECT MAX("Id") FROM "ProductChunks"), 0) + 1,
                          false);
            """;

        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Seed file not found: {path}");

        await using var stream = File.OpenRead(path);

        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);

        return result ?? throw new InvalidOperationException($"Failed to deserialize {path}");
    }

    private async Task<int> InsertCategoriesAsync(List<CategorySeed> seeds, CancellationToken ct)
    {
        var existingIds = await db.Categories
            .Select(c => c.Id)
            .ToHashSetAsync(ct);

        var toInsert = seeds
            .Where(s => !existingIds.Contains(s.Id))
            .Select(s => new ProductCategory
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            })
            .ToList();

        if (toInsert.Count == 0)
            return 0;

        db.Categories.AddRange(toInsert);
        await db.SaveChangesAsync(ct);

        return toInsert.Count;
    }

    private async Task<int> InsertTagsAsync(List<TagSeed> seeds, CancellationToken ct)
    {
        var existingIds = await db.Tags
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        var toInsert = seeds
            .Where(s => !existingIds.Contains(s.Id))
            .Select(s => new Tag { Id = s.Id, Name = s.Name })
            .ToList();

        if (toInsert.Count == 0)
            return 0;

        db.Tags.AddRange(toInsert);
        await db.SaveChangesAsync(ct);

        return toInsert.Count;
    }

    private async Task<(int products, int documents)> InsertProductsAsync(
        List<ProductSeed> seeds,
        CancellationToken ct)
    {
        var existingProductIds = await db.Products
            .Select(p => p.Id)
            .ToHashSetAsync(ct);

        var existingTagIds = await db.Tags
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        var newProducts = new List<Product>();

        foreach (var s in seeds)
        {
            if (existingProductIds.Contains(s.Id))
                continue;

            var p = new Product
            {
                Id = s.Id,
                Name = s.Name,
                Summary = s.Summary,
                SKU = s.Sku,
                Description = s.Description,
                IsActive = true,
                Price = s.Price,
                StockCount = s.StockCount,
                ProductCategoryId = s.ProductCategoryId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var tagId in s.TagIds.Where(existingTagIds.Contains).Distinct())
                p.ProductTags.Add(new ProductTag { TagId = tagId });

            foreach (var d in s.Documents)
            {
                p.Documents.Add(new ProductDocument
                {
                    Type = d.Type,
                    Title = d.Title,
                    Content = d.Content,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            newProducts.Add(p);
        }

        if (newProducts.Count == 0)
            return (0, 0);

        db.Products.AddRange(newProducts);
        await db.SaveChangesAsync(ct);

        var docCount = newProducts.Sum(p => p.Documents.Count);
        return (newProducts.Count, docCount);
    }
}