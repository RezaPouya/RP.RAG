namespace RP.Rag.Domain.Entities.Products;

public sealed class Product
{
    public int Id { get; set; }
    public int ProductCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal Price { get; set; }
    public int StockCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ProductSync? Sync { get; set; }
    public ProductCategory Category { get; set; } = null!;

    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    public ICollection<ProductDocument> Documents { get; set; } = new List<ProductDocument>();
    public ICollection<ProductChunk> Chunks { get; set; } = new List<ProductChunk>();

    /// <summary>
    /// for test 
    /// </summary>
    public void SetToInitialState()
    {
        this.IsActive = true;
        this.Sync.SetToInitialState();
    }
}