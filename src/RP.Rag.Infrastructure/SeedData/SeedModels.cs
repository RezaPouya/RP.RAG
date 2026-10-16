using System.Text.Json.Serialization;

namespace RP.Rag.Infrastructure.SeedData;

public sealed class CategorySeed
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public sealed class TagSeed
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class ProductSeed
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("productCategoryId")] public int ProductCategoryId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sku")] public string Sku { get; set; } = "";
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; } = true;
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("stockCount")] public int StockCount { get; set; }
    [JsonPropertyName("tagIds")] public List<int> TagIds { get; set; } = new();
    [JsonPropertyName("documents")] public List<DocumentSeed> Documents { get; set; } = new();
}

public sealed class DocumentSeed
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}