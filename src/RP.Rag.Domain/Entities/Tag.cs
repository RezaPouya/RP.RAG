using RP.Rag.Domain.Entities.Products;

namespace RP.Rag.Domain.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}