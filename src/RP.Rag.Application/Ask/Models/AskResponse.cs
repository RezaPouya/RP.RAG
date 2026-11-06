using RP.RAG.Application.Catalog;

namespace RP.Rag.Application.Ask.Models;

public sealed class AskResponse
{
    public Guid ConversationId { get; init; }
    public int ConversationCount { get; init; }
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
    public string? Model { get; init; }
    public long TookMs { get; init; }
    public bool IsBlocked { get; init; }
    public string? BlockReason { get; init; }

    // Read from PostgreSQL. [n] in the answer points to the n-th card.
    public IReadOnlyList<ProductCard> Products { get; init; } = [];

    public IReadOnlyList<AskSource> Sources { get; init; } = [];
}
