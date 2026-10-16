using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.RAG.Infrastructure.Elastic.Models;

namespace RP.RAG.Infrastructure.Elastic;

public sealed class ProductIndex(
    ElasticsearchClientFactory factory,
    IOptions<ElasticsearchOptions> options,
    ILogger<ProductIndex> logger)
{
    private ElasticsearchClient Client => factory.Create();
    private string IndexName => options.Value.ProductIndexName;

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        var exists = await Client.Indices.ExistsAsync(IndexName, ct);

        if (exists.Exists)
        {
            logger.LogInformation("Index '{Index}' already exists.", IndexName);
            return;
        }

        var response = await Client.Indices.CreateAsync<ProductSearchDocument>(
            IndexName,
            d => d
                .Settings(s => s
                    .Analysis(a => a
                        .CharFilters(cf => cf
                            .Mapping("zero_width_spaces", m => m
                                .Mappings(new[] { "\\u200C=>\\u0020" })))
                        .TokenFilters(tf => tf
                            .Stop("english_stop", sf => sf
                                .Stopwords(StopWordLanguage.English))
                            .Stemmer("english_stemmer", sf => sf
                                .Language("english"))
                            .Stop("persian_stop", sf => sf
                                .Stopwords(StopWordLanguage.Persian))
                            .PatternReplace("persian_plural_suffix", pr => pr
                                .Pattern("^(.{3,}?)(?:\u0647\u0627\u06CC|\u0647\u0627)$")
                                .Replacement("$1")))
                        .Analyzers(an => an
                            .Custom("english_custom", ca => ca
                                .Tokenizer("standard")
                                .Filter(new[]
                                {
                                    "lowercase",
                                    "english_stop",
                                    "english_stemmer"
                                }))
                            .Custom("persian_custom", ca => ca
                                .CharFilter(new[] { "zero_width_spaces" })
                                .Tokenizer("standard")
                                .Filter(new[]
                                {
                                    "lowercase",
                                    "decimal_digit",
                                    "arabic_normalization",
                                    "persian_normalization",
                                    "persian_stop",
                                    "persian_plural_suffix"
                                })))))
                .Mappings(m => m
                    .Properties(p => p
                        .IntegerNumber(x => x.ProductId)

                        .Keyword(x => x.Sku)

                        .Text(x => x.Name, t => t
                            .Analyzer("english_custom")
                            .Fields(f => f
                                .Keyword("keyword")
                                .Text("fa", fa => fa.Analyzer("persian_custom"))))

                        .Text(x => x.Summary, t => t
                            .Analyzer("english_custom")
                            .Fields(f => f
                                .Text("fa", fa => fa.Analyzer("persian_custom"))))

                        .Text(x => x.Description, t => t
                            .Analyzer("english_custom")
                            .Fields(f => f
                                .Text("fa", fa => fa.Analyzer("persian_custom"))))

                        .IntegerNumber(x => x.CategoryId)

                        .Text(x => x.CategoryName, t => t
                            .Analyzer("english_custom")
                            .Fields(f => f
                                .Keyword("keyword")
                                .Text("fa", fa => fa.Analyzer("persian_custom"))))

                        .Keyword(x => x.Tags)

                        .Text(x => x.SearchText, t => t
                            .Analyzer("english_custom")
                            .Fields(f => f
                                .Text("fa", fa => fa.Analyzer("persian_custom"))))

                        .Keyword(x => x.SearchTextHash)

                        .Date(x => x.UpdatedAt))),
            ct);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Failed to create index '{IndexName}': {response.DebugInformation}");
        }

        logger.LogInformation("Index '{Index}' created.", IndexName);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        var response = await Client.Indices.DeleteAsync(IndexName, ct);

        if (response.IsValidResponse)
            logger.LogInformation("Index '{Index}' deleted.", IndexName);
        else
            logger.LogWarning("Failed to delete index: {Debug}", response.DebugInformation);
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        var response = await Client.CountAsync<ProductSearchDocument>(
            c => c.Indices(IndexName), ct);
        return response.Count;
    }
}