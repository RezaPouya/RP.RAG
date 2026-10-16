using RP.RAG.Application.Search.Models;

namespace RP.RAG.Application.Search;

public interface IHybridSearch
{
    Task<SearchResult> SearchAsync(
        string query,
        int topK = 10,
        bool onlyActive = true,
        CancellationToken ct = default);

    Task<HybridRetrieval> RetrieveAsync(
        string query,
        int topProducts = 5,
        int chunksPerProduct = 2,
        bool onlyActive = true,
        CancellationToken ct = default);
}
