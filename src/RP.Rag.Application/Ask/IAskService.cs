namespace RP.RAG.Application.Ask;

public interface IAskService
{
    Task<AskResponse> AskAsync(
        string question,
        int? topProducts = null,
        Guid? conversationId = null,
        CancellationToken ct = default);
}
