using Microsoft.Extensions.Options;
using RP.RAG.Application.Ask;
using RP.RAG.Infrastructure.Embeddings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RP.RAG.Infrastructure.Ask;

public sealed class DeepSeekAnswerGenerator(IHttpClientFactory httpFactory, IOptions<DeepSeekOptions> options) : IAnswerGenerator
{
    private readonly HttpClient _http = httpFactory.CreateClient("DeepSeek");
    private readonly DeepSeekOptions _options = options.Value;
    public string ModelName => _options.GenerationModel;

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = _options.GenerationModel,
            Stream = false,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ]
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"DeepSeek returned {(int)response.StatusCode}: {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("DeepSeek returned an empty answer.");

        return content.Trim();
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = new();
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }
}