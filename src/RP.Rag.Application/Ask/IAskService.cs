namespace RP.RAG.Application.Ask;

public interface IAskService
{
    Task<AskResponse> AskAsync(
        string question,
        int? topProducts = null,
        CancellationToken ct = default);
}
