namespace RP.Rag.Infrastructure.Layas;

public sealed class LayaOptions
{
    public const string SectionName = "Laya";

    public string Url { get; set; } = "http://localhost:8090";

    public string? ApiKey { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 15;

    public double JailbreakThreshold { get; set; } = 0.85;

    public double PromptInjectionThreshold { get; set; } = 0.80;

    public double PolicyViolationThreshold { get; set; } = 0.70;
}