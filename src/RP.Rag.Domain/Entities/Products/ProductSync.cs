namespace RP.Rag.Domain.Entities.Products;

// Sync state for a product, one row per product. Workers read this table instead of Products.
public sealed class ProductSync
{
    public int ProductId { get; set; }

    public bool ContentChanged { get; set; } = true;

    public bool EmbeddingChanged { get; set; } = true;

    public DateTime? ContentSyncedAt { get; set; }
    public DateTime? EmbeddingSyncedAt { get; set; }

    public int ContentSyncRetries { get; set; }
    public int EmbeddingSyncRetries { get; set; }

    public string? ContentSyncError { get; set; }
    public string? EmbeddingSyncError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;

    public static ProductSync Default(int productId)
    {
        return new ProductSync
        {
            ProductId = productId,
            ContentChanged = true,
            EmbeddingChanged = false,
            ContentSyncedAt = null,
            EmbeddingSyncedAt = null,
            ContentSyncRetries = 0,
            EmbeddingSyncRetries = 0,
            ContentSyncError = null,
            EmbeddingSyncError = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    internal void SetToInitialState()
    {
        ContentChanged = true;
        EmbeddingChanged = false;
        ContentSyncedAt = null;
        EmbeddingSyncedAt = null;
        ContentSyncRetries = 0;
        EmbeddingSyncRetries = 0;
        ContentSyncError = null;
        EmbeddingSyncError = null;
    }
}