namespace RP.Rag.Domain.Entities.Products;

public sealed class ProductDocument
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>"warranty" | "return" | "manual" | "spec" | "faq"</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductChunk> Chunks { get; set; } = new List<ProductChunk>();
}