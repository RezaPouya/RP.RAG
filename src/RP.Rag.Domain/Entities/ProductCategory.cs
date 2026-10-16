using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Domain.Entities;

public sealed class ProductCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}