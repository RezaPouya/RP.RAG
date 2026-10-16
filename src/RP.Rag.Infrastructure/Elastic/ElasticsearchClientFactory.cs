using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RP.RAG.Infrastructure.Elastic;

public sealed class ElasticsearchClientFactory
{
    private readonly ElasticsearchClient _client;

    public ElasticsearchClientFactory(
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchClientFactory> logger)
    {
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.Url))
            throw new InvalidOperationException(
                "Elasticsearch:Url is not configured.");

        if (!Uri.TryCreate(opts.Url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException(
                $"Elasticsearch:Url is invalid: '{opts.Url}'.");

        if (opts.RequestTimeoutSeconds <= 0)
            throw new InvalidOperationException(
                "Elasticsearch:RequestTimeoutSeconds must be greater than zero.");

        if (string.IsNullOrWhiteSpace(opts.ProductIndexName))
            throw new InvalidOperationException(
                "Elasticsearch:ProductIndexName is not configured.");

        var settings = new ElasticsearchClientSettings(uri)
            .RequestTimeout(
                TimeSpan.FromSeconds(opts.RequestTimeoutSeconds))
            .DefaultIndex(opts.ProductIndexName);

        _client = new ElasticsearchClient(settings);

        logger.LogInformation(
            "Elasticsearch client created. Url={Url}, DefaultIndex={Index}, Timeout={Timeout}s",
            opts.Url,
            opts.ProductIndexName,
            opts.RequestTimeoutSeconds);
    }

    public ElasticsearchClient Create() => _client;
}