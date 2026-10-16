using System.Text.Json.Serialization;

namespace RP.RAG.Infrastructure.Embeddings;

public sealed partial class OllamaEmbeddingGenerator
{
    private sealed class OllamaEmbedRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("input")] public List<string> Input { get; set; } = new();
    }
}
