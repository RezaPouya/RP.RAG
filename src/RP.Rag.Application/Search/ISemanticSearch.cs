using RP.RAG.Application.Search.Models;

namespace RP.RAG.Application.Search;

public interface ISemanticSearch
{
    Task<SearchResult> SearchAsync(
        string query,
        int topK = 10,
        bool onlyActive = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<ChunkHit>> SearchChunksAsync(
        string query,
        int limit = 30,
        bool onlyActive = true,
        CancellationToken ct = default);
}
