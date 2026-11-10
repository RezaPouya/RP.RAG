namespace RP.Rag.Application.Guardrails;

public interface IPromptGuardrail
{
    Task<PromptGuardrailResult> EvaluateAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}