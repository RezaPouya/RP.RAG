namespace RP.RAG.Application.Ask;

public interface IAnswerGenerator
{
    string ModelName { get; }

    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);
}
