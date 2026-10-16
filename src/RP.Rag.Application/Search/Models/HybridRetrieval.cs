namespace RP.RAG.Application.Search.Models;

public sealed record HybridRetrieval(
    string Query,
    long TookMs,
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<ChunkHit> Chunks)
{
    public static HybridRetrieval Empty(string query) => new(query, 0, [], []);
}
