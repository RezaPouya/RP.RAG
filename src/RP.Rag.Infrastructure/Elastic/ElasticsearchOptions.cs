namespace RP.RAG.Infrastructure.Elastic;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; set; } = "http://localhost:9200";
    public string ProductIndexName { get; set; } = "products";
    public int RequestTimeoutSeconds { get; set; } = 30;
}