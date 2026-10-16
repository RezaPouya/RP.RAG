using Pgvector;

namespace RP.Rag.Domain.Entities.Products;

public sealed class ProductChunk
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? DocumentId { get; set; }
    public ProductDocument? Document { get; set; }

    /// <summary>"summary" | "description" | "spec" | "document"</summary>
    public string ChunkType { get; set; } = string.Empty;

    public int Ordinal { get; set; }
    public string? Header { get; set; }

    public string ChunkText { get; set; } = string.Empty;
    public string EmbeddingText { get; set; } = string.Empty;
    public string ChunkTextHash { get; set; } = string.Empty;

    public Vector? Embedding { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingVersion { get; set; }

    /// <summary>"Pending" | "Completed" | "Failed" | "Skipped"</summary>
    public string EmbeddingStatus { get; set; } = "Pending";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}