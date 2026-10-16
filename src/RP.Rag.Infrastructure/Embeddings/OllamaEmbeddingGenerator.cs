using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using RP.RAG.Application.Embeddings;
using System.Net.Http.Json;

namespace RP.RAG.Infrastructure.Embeddings;

public sealed partial class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private const int ExpectedDimensions = 1024;

    private readonly HttpClient _http;
    private readonly ILogger<OllamaEmbeddingGenerator> _logger;
    private readonly string _modelName;

    public int Dimensions => ExpectedDimensions;
    public string ModelName => _modelName;

    public OllamaEmbeddingGenerator(
        IHttpClientFactory httpFactory,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingGenerator> logger)
    {
        _http = httpFactory.CreateClient("Ollama");
        _logger = logger;
        _modelName = options.Value.EmbeddingModel;

        _logger.LogInformation(
            "OllamaEmbeddingGenerator configured. Model={Model}, Dims={Dims}",
            _modelName, ExpectedDimensions);
    }

    public async Task<Vector> GenerateAsync(string text, CancellationToken ct = default)
    {
        var results = await GenerateBatchAsync(new[] { text }, ct);
        return results[0];
    }

    public async Task<Vector[]> GenerateBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts.Count == 0)
            return Array.Empty<Vector>();

        var request = new OllamaEmbedRequest
        {
            Model = _modelName,
            Input = texts.ToList()
        };

        using var response = await _http.PostAsJsonAsync(
            "/api/embed", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Ollama returned {response.StatusCode}: {body}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<OllamaEmbedResponse>(ct);

        if (result?.Embeddings is null
            || result.Embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Ollama returned {result?.Embeddings?.Count ?? 0} " +
                $"embeddings for {texts.Count} inputs.");
        }

        var vectors = new Vector[result.Embeddings.Count];

        for (var i = 0; i < result.Embeddings.Count; i++)
        {
            var v = result.Embeddings[i];

            if (v.Length != ExpectedDimensions)
                throw new InvalidOperationException(
                    $"Expected {ExpectedDimensions} dims, got {v.Length}.");

            vectors[i] = new Vector(v);
        }

        return vectors;
    }
}