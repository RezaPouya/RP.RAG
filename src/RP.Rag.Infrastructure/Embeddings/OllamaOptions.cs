namespace RP.RAG.Infrastructure.Embeddings;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Url { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "bge-m3:latest";
    public string GenerationModel { get; set; } = "gemma4:e4b";
    public double Temperature { get; set; } = 0.2;
    public int RequestTimeoutSeconds { get; set; } = 120;
}
