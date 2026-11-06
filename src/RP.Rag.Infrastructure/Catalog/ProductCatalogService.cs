using Microsoft.EntityFrameworkCore;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Catalog;

namespace RP.RAG.Infrastructure.Catalog;

public sealed class ProductCatalogService(RpRagDbContext db) : IProductCatalogService
{
    public async Task<IReadOnlyDictionary<int, ProductCard>> GetCardsAsync(IReadOnlyCollection<int> productIds,
        bool onlyActive = true, CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, ProductCard>();

        var ids = productIds.ToList();

        var cards = await db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Where(p => !onlyActive || p.IsActive)
            .Select(p => new ProductCard
            {
                ProductId = p.Id,
                Sku = p.SKU,
                Name = p.Name,
                Summary = p.Summary,
                CategoryName = p.Category.Name,
                Tags = p.ProductTags.Select(pt => pt.Tag.Name).ToList(),
                Price = p.Price,
                StockCount = p.StockCount,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        return cards.ToDictionary(c => c.ProductId);
    }
}
