namespace RP.RAG.Application.Ask;

/// <summary>
/// Evaluates the complete user request and assembled context before generation.
/// </summary>
public interface IFinalPromptGuardrail
{
    Task<FinalPromptGuardrailResult> EvaluateAsync(
        string systemPrompt,
        string assembledPrompt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains Stage 3 policy decisions for the assembled request.
/// </summary>
public sealed record FinalPromptGuardrailResult(
    bool IsAllowed,
    double JailbreakProbability,
    double PromptInjectionProbability,
    string? BlockReason = null);
