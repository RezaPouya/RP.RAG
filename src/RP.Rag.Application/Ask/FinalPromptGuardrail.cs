namespace RP.RAG.Application.Ask;

/// <summary>
/// Evaluates the complete user request and assembled context before generation.
/// </summary>
public interface IFinalPromptGuardrail
{
    Task<FinalPromptGuardrailResult> EvaluateAsync(
        string systemPrompt,
        string assembledPrompt,
        CancellationToken cancellationToken = default,
        string? originalQuestion = null);
}

/// <summary>
/// Contains input-policy and assembled-request decisions returned by the combined Laya call.
/// </summary>
public sealed record FinalPromptGuardrailResult(
    bool IsAllowed,
    double JailbreakProbability,
    double PromptInjectionProbability,
    string? BlockReason = null,
    bool IsUserPromptBlocked = false,
    double UserJailbreakProbability = 0,
    double UserPromptInjectionProbability = 0,
    double PolicyViolationProbability = 0);
