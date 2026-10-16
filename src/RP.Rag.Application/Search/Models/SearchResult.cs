namespace RP.RAG.Application.Search.Models;

public sealed class SearchResult
{
    public string Query { get; init; } = "";
    public string Strategy { get; init; } = "Lexical";
    public long Total { get; init; }
    public long TookMs { get; init; }
    public IReadOnlyList<SearchHit> Hits { get; init; } = [];
}