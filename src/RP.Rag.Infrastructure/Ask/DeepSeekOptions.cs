namespace RP.RAG.Infrastructure.Embeddings;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string Url { get; set; } = "https://api.deepseek.com";
    public string ApiKey { get; set; } = "";
    public string GenerationModel { get; set; } = "deepseek-flash";
    public double Temperature { get; set; } = 0.2;
    public int RequestTimeoutSeconds { get; set; } = 120;
}