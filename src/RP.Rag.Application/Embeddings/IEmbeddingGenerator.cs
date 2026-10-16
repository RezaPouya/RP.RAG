using Pgvector;

namespace RP.RAG.Application.Embeddings;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }
    string ModelName { get; }

    Task<Vector> GenerateAsync(string text, CancellationToken ct = default);

    Task<Vector[]> GenerateBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}