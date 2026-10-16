using RP.Rag.Domain.Entities.Products;

namespace RP.RAG.Infrastructure.Elastic.Models;

public static class ProductSearchDocumentMapper
{
    public static ProductSearchDocument ToDocument(Product p)
    {
        var tags = p.ProductTags.Select(pt => pt.Tag.Name).ToList();

        var searchText = ProductSearchTextBuilder.Build(
            p.SKU,
            p.Name,
            p.Summary,
            p.Description,
            p.Category?.Name,
            tags);

        return new ProductSearchDocument
        {
            ProductId = p.Id,
            Sku = p.SKU,
            Name = p.Name,
            Summary = p.Summary,
            Description = p.Description,
            CategoryId = p.ProductCategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            Tags = tags,
            SearchText = searchText,
            UpdatedAt = p.UpdatedAt
        };
    }
}