using RP.RAG.Application.Catalog;

namespace RP.RAG.Application.Ask;

public sealed class AskResponse
{
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
    public string? Model { get; init; }
    public long TookMs { get; init; }

    // Read from PostgreSQL. [n] in the answer points to the n-th card.
    public IReadOnlyList<ProductCard> Products { get; init; } = [];

    public IReadOnlyList<AskSource> Sources { get; init; } = [];
}

public sealed class AskSource
{
    public int ProductRef { get; init; }
    public int ProductId { get; init; }
    public string ChunkType { get; init; } = "";
    public string? Header { get; init; }
}
