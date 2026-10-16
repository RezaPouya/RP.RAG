namespace RP.RAG.Application.Search.Models;

public sealed class SearchHit
{
    public int ProductId { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string CategoryName { get; init; } = "";
    public List<string> Tags { get; init; } = new();
    public double Score { get; init; }
    public string? Highlight { get; init; }

    public decimal Price { get; init; }
    public bool IsActive { get; set; }
    public int StockCount { get; set; }
}
