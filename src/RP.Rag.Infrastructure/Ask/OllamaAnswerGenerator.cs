using Microsoft.Extensions.Options;
using RP.RAG.Application.Ask;
using RP.RAG.Infrastructure.Embeddings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RP.RAG.Infrastructure.Ask;

public sealed class OllamaAnswerGenerator : IAnswerGenerator
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public string ModelName => _options.GenerationModel;

    public OllamaAnswerGenerator(IHttpClientFactory httpFactory, IOptions<OllamaOptions> options)
    {
        _http = httpFactory.CreateClient(ServiceKeys.Ollama);
        _options = options.Value;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = _options.GenerationModel,
            Stream = false,
            Options = new ChatOptions { Temperature = _options.Temperature },
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ]
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Ollama returned {(int)response.StatusCode}: {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);
        var content = result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Ollama returned an empty answer.");

        return content.Trim();
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = new();
        [JsonPropertyName("options")] public ChatOptions? Options { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class ChatOptions
    {
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }
}
