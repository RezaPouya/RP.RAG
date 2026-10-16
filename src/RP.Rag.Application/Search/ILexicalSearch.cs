using RP.RAG.Application.Search.Models;

namespace RP.RAG.Application.Search;

public interface ILexicalSearch
{
    Task<SearchResult> SearchAsync(
        string query,
        int topK = 10,
        bool onlyActive = true,
        CancellationToken ct = default);
}