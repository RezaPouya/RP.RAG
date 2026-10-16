using System.Text.Json.Serialization;

namespace RP.RAG.Infrastructure.Embeddings;

public sealed partial class OllamaEmbeddingGenerator
{
    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]> Embeddings { get; set; } = new();
    }
}
