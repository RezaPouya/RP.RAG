using System.Text.Json.Serialization;

namespace RP.RAG.Infrastructure.Elastic.Models;

public sealed class ProductSearchDocument
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("categoryId")]
    public int CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("searchText")]
    public string SearchText { get; set; } = "";

    [JsonPropertyName("searchTextHash")]
    public string SearchTextHash { get; set; } = "";


    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

}