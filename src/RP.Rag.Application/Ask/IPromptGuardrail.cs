namespace RP.RAG.Application.Ask;

/// <summary>
/// Evaluates an incoming user prompt before retrieval or generation.
/// </summary>
public interface IPromptGuardrail
{
    Task<PromptGuardrailResult> EvaluateAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains the guardrail assessment and the policy decision.
/// </summary>
public sealed record PromptGuardrailResult(
    bool IsAllowed,
    double JailbreakProbability,
    double PromptInjectionProbability,
    string? BlockReason = null);
