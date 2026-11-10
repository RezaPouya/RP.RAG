namespace RP.Rag.Application.Guardrails;

/// <summary>
/// Contains the guardrail assessment and the policy decision.
/// </summary>
public sealed record PromptGuardrailResult(
    bool IsAllowed,
    double JailbreakProbability,
    double PromptInjectionProbability,
    string? BlockReason = null,
    double OutOfScopeProbability = 0,
    double RestrictedContentProbability = 0,
    double ExternalSourceProbability = 0);
