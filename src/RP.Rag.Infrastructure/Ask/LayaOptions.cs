namespace RP.RAG.Infrastructure.Ask;

public sealed class LayaOptions
{
    public const string SectionName = "Laya";

    public string Url { get; set; } = "http://localhost:8090";
    public string? ApiKey { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;
    public double JailbreakThreshold { get; set; } = 0.5;
    public double PromptInjectionThreshold { get; set; } = 0.5;
    public double ChunkPromptInjectionThreshold { get; set; } = 0.5;
    public double FinalJailbreakThreshold { get; set; } = 0.5;
    public double FinalPromptInjectionThreshold { get; set; } = 0.5;
}
