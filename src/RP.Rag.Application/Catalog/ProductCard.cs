namespace RP.RAG.Application.Catalog;

public sealed class ProductCard
{
    public int ProductId { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Summary { get; init; }
    public string CategoryName { get; init; } = "";
    public List<string> Tags { get; init; } = new();
    public decimal Price { get; init; }
    public int StockCount { get; init; }
    public bool IsActive { get; init; }
    public bool InStock => StockCount > 0;
}
