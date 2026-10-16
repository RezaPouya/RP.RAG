using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Search;
using RP.RAG.Application.Search.Models;
using RP.RAG.Infrastructure.Elastic;
using RP.RAG.Infrastructure.Elastic.Models;

namespace RP.Rag.Infrastructure.Search;

public sealed class LexicalSearchService(
    ElasticsearchClientFactory factory,
    IDbContextFactory<RpRagDbContext> dbFactory,
    IOptions<ElasticsearchOptions> options,
    ILogger<LexicalSearchService> logger) : ILexicalSearch
{
    private const string StrategyName = "Lexical";

    private const int MaxCandidateMultiplier = 5;
    private const int MinCandidateCount = 50;
    private const int MaxCandidateCount = 200;

    private const int ExactNameBoost = 100;
    private const int ExactSkuBoost = 100;
    private const int ExactCategoryBoost = 50;
    private const int NamePhraseBoost = 25;
    private const int SummaryPhraseBoost = 40;
    private const int NamePartialBoost = 10;
    private const int SummaryPartialBoost = 15;
    private const int MultiMatchBoost = 1;
    private const int TagsBoost = 150;

    private const string PartialMatchRule = "2<75%";
    private const string SingleTermRule = "1";

    private static readonly string[] MultiMatchFields =
    [
        "name^8",
        "summary^3",
        "description^1",
        "searchText^0.5"
    ];

    public async Task<SearchResult> SearchAsync(
        string query,
        int topK = 10,
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return EmptyResult(query);
        }

        query = query.Trim();

        var indexName = options.Value.ProductIndexName;
        var client = factory.Create();

        var candidateSize = ResolveCandidateSize(topK, onlyActive);

        var response = await client.SearchAsync<ProductSearchDocument>(
            s => s.Indices(indexName)
                .Size(candidateSize)
                .TrackTotalHits(true)
                .Query(q => BuildLexicalQuery(q, query))
                .Source(src => src.Filter(f => f.Includes(Infer.Fields<ProductSearchDocument>(p => p.ProductId)))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            logger.LogError(
                "Elasticsearch lexical search failed. Query={Query}, Index={Index}, Debug={Debug}",
                query,
                indexName,
                response.DebugInformation);

            throw new InvalidOperationException(
                $"Elasticsearch lexical search failed. {response.DebugInformation}");
        }

        var rankedHits = ExtractRankedHits(response);

        if (rankedHits.Count == 0)
        {
            return ResultWithoutHits(query, response);
        }

        var products = await LoadProductsAsync(rankedHits, onlyActive, cancellationToken);

        var hits = BuildHits(rankedHits, products, topK);

        logger.LogInformation(
            "Lexical search '{Query}': total={Total}, returned={Returned}, {Ms}ms",
            query,
            response.Total,
            hits.Count,
            response.Took);

        return new SearchResult
        {
            Query = query,
            Strategy = StrategyName,
            Total = response.Total,
            TookMs = response.Took,
            Hits = hits
        };
    }

    private static int ResolveCandidateSize(int topK, bool onlyActive)
    {
        if (!onlyActive)
        {
            return Math.Min(topK, MaxCandidateCount);
        }

        return Math.Min(
            Math.Max(topK * MaxCandidateMultiplier, MinCandidateCount),
            MaxCandidateCount);
    }

    private static List<(int ProductId, double? Score)> ExtractRankedHits(
        SearchResponse<ProductSearchDocument> response)
    {
        var rankedHits = new List<(int ProductId, double? Score)>();
        var seen = new HashSet<int>();

        foreach (var hit in response.Hits)
        {
            var productId = int.TryParse(hit.Id, out var idFromDocument)
                ? idFromDocument
                : hit.Source?.ProductId ?? 0;

            if (productId > 0 && seen.Add(productId))
            {
                rankedHits.Add((productId, hit.Score));
            }
        }

        return rankedHits;
    }

    private async Task<Dictionary<int, Product>> LoadProductsAsync(
        List<(int ProductId, double? Score)> rankedHits,
        bool onlyActive,
        CancellationToken cancellationToken)
    {
        var rankedIds = rankedHits.Select(x => x.ProductId).ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => rankedIds.Contains(p.Id))
            .Where(p => !onlyActive || p.IsActive)
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private static List<SearchHit> BuildHits(
        List<(int ProductId, double? Score)> rankedHits,
        Dictionary<int, Product> products,
        int topK)
    {
        var hits = new List<SearchHit>(Math.Min(topK, products.Count));

        foreach (var (productId, score) in rankedHits)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                continue;
            }

            hits.Add(new SearchHit
            {
                ProductId = product.Id,
                Sku = product.SKU,
                Name = product.Name,
                Summary = product.Summary,
                Description = product.Description,
                CategoryName = product.Category?.Name ?? string.Empty,
                Tags = product.ProductTags.Select(pt => pt.Tag.Name).ToList(),
                Price = product.Price,
                StockCount = product.StockCount,
                IsActive = product.IsActive,
                Score = score ?? 0
            });

            if (hits.Count >= topK)
            {
                break;
            }
        }

        return hits;
    }

    private static SearchResult EmptyResult(string query) => new()
    {
        Query = query,
        Strategy = StrategyName,
        Total = 0,
        TookMs = 0,
        Hits = []
    };

    private static SearchResult ResultWithoutHits(
        string query,
        SearchResponse<ProductSearchDocument> response) => new()
        {
            Query = query,
            Strategy = StrategyName,
            Total = response.Total,
            TookMs = response.Took,
            Hits = []
        };

    private static Query BuildLexicalQuery(
        QueryDescriptor<ProductSearchDocument> q,
        string query)
    {
        var queryTerms = query
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tagTerms = queryTerms
            .Select(t => FieldValue.String(t))
            .ToArray();

        var outerMinimumShouldMatch = queryTerms.Length > 1 ? 2 : 1;

        return q.Bool(b =>
        {
            b.Must(q => q.Bool(mb => mb
                .Should(
                    q => q.Match(m => m.Field("name").Query(query).MinimumShouldMatch(SingleTermRule)),
                    q => q.Match(m => m.Field("categoryName").Query(query).MinimumShouldMatch(SingleTermRule)),
                    q => q.Match(m => m.Field("summary").Query(query).MinimumShouldMatch(PartialMatchRule)))
                .MinimumShouldMatch(1)));

            b.Should(
                q => q.Term(t => t.Field("name.keyword").Value(query).CaseInsensitive(true).Boost(ExactNameBoost)),
                q => q.Term(t => t.Field("sku").Value(query).CaseInsensitive(true).Boost(ExactSkuBoost)),
                q => q.Term(t => t.Field("categoryName.keyword").Value(query).CaseInsensitive(true).Boost(ExactCategoryBoost)),
                q => q.MatchPhrase(mp => mp.Field("name").Query(query).Boost(NamePhraseBoost)),
                q => q.MatchPhrase(mp => mp.Field("summary").Query(query).Boost(SummaryPhraseBoost)),
                q => q.Match(m => m.Field("name").Query(query).MinimumShouldMatch(PartialMatchRule).Boost(NamePartialBoost)),
                q => q.Match(m => m.Field("summary").Query(query).MinimumShouldMatch(PartialMatchRule).Boost(SummaryPartialBoost)),
                q => q.MultiMatch(mm => mm
                    .Query(query)
                    .Fields(MultiMatchFields)
                    .Type(TextQueryType.BestFields)
                    .MinimumShouldMatch(PartialMatchRule)
                    .Boost(MultiMatchBoost)),
                q => q.Terms(t => t.Field("tags").Terms(new TermsQueryField(tagTerms)).Boost(TagsBoost)));

            b.MinimumShouldMatch(outerMinimumShouldMatch);
        });
    }
}