namespace RP.RAG.Application.Ask;

public enum ChunkRelevance
{
    Relevant,
    PartiallyRelevant,
    Irrelevant
}

/// <summary>
/// Laya's decisions about a retrieved chunk in relation to the original question.
/// </summary>
public sealed record ChunkRelevanceResult(
    long ChunkId,
    ChunkRelevance Relevance,
    double PromptInjectionProbability,
    bool ContainsPromptInjection);

/// <summary>
/// Assesses retrieved chunks before they are assembled into the generation context.
/// </summary>
public interface IChunkRelevanceEvaluator
{
    Task<IReadOnlyList<ChunkRelevanceResult>> EvaluateAsync(
        string question,
        IReadOnlyList<RP.RAG.Application.Search.Models.ChunkHit> chunks,
        CancellationToken cancellationToken = default);
}
