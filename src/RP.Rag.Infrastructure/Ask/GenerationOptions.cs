namespace RP.RAG.Infrastructure.Ask;

public sealed class GenerationOptions
{
    public const string SectionName = "Generation";

    /// <summary>
    /// "Ollama" or "DeepSeek".
    /// </summary>
    public string Provider { get; set; } = "Ollama";
}