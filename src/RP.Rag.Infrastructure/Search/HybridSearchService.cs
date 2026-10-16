using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Catalog;
using RP.RAG.Application.Search;
using RP.RAG.Application.Search.Models;
using System.Diagnostics;

namespace RP.RAG.Infrastructure.Search;

public sealed class HybridSearchService(
    ILexicalSearch lexical,
    ISemanticSearch semantic,
    IProductCatalogService catalog,
    IDbContextFactory<RpRagDbContext> dbFactory,
    CategoryTokenIndexCache categoryIndexCache,
    ILogger<HybridSearchService> logger) : IHybridSearch
{
    private const string StrategyName = "Hybrid";

    private const int MinCandidates = 20;
    private const int MaxCandidates = 100;
    private const int CandidateMultiplier = 4;
    private const int ChunkFetchMultiplier = 3;

    private const int MinTopProducts = 1;
    private const int MaxTopProducts = 20;
    private const int MinChunksPerProduct = 1;
    private const int MaxChunksPerProduct = 5;

    private const double LexicalRankFusionK = 60.0;
    private const double NameTokenOverlapBoost = 0.005;
    private const double CategoryMatchBoost = 0.020;
    private const double CategoryMismatchPenalty = 0.010;
    private const double TagMatchBoost = 0.010;

    private static readonly string[] LeadChunkTypes = ["summary", "description"];

    public async Task<HybridRetrieval> RetrieveAsync(
        string query,
        int topProducts = 5,
        int chunksPerProduct = 2,
        bool onlyActive = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return HybridRetrieval.Empty(query);
        }

        topProducts = Math.Clamp(topProducts, MinTopProducts, MaxTopProducts);
        chunksPerProduct = Math.Clamp(chunksPerProduct, MinChunksPerProduct, MaxChunksPerProduct);

        var candidateCount = ResolveCandidateCount(topProducts);

        var totalSw = Stopwatch.StartNew();

        var lexicalTask = RunLexicalAsync(query, candidateCount, onlyActive, ct);
        var semanticTask = RunSemanticAsync(query, candidateCount * ChunkFetchMultiplier, onlyActive, ct);

        await Task.WhenAll(lexicalTask, semanticTask);

        var lexicalResult = await lexicalTask;
        var semanticChunks = await semanticTask;

        var lexicalIds = lexicalResult.Hits.Select(h => h.ProductId).ToList();

        var semanticProductIds = semanticChunks
            .Select(c => c.ProductId)
            .ToHashSet();

        var lexicalOnlyIds = lexicalIds
            .Take(topProducts * 2)
            .Where(id => !semanticProductIds.Contains(id))
            .ToList();

        var lexicalOnlyChunks = lexicalOnlyIds.Count == 0
            ? new List<ChunkHit>()
            : await LoadLeadChunksAsync(lexicalOnlyIds, lexicalIds, onlyActive, ct);

        var fused = HybridRanker.Fuse(semanticChunks, lexicalIds, lexicalOnlyChunks);

        var withCategorySignal = await ApplyCategorySignalAsync(fused, query, ct);
        var withTagSignal = await ApplyTagSignalAsync(withCategorySignal, query, ct);
        var withNameBoost = await ApplyNameTokenBoostAsync(withTagSignal, query, ct);

        var ranked = withNameBoost;

        var groups = ranked
            .GroupBy(c => c.ProductId)
            .Take(topProducts)
            .ToList();

        var productIds = groups.Select(g => g.Key).ToList();

        var chunks = groups
            .SelectMany(g => g
                .OrderByDescending(c => c.Score)
                .Take(chunksPerProduct))
            .ToList();

        totalSw.Stop();

        logger.LogInformation(
            "Hybrid retrieval '{Query}': products={ProductCount}, chunks={ChunkCount}, total={TotalMs}ms",
            query,
            productIds.Count,
            chunks.Count,
            totalSw.ElapsedMilliseconds);

        return new HybridRetrieval(query, totalSw.ElapsedMilliseconds, productIds, chunks);
    }

    public async Task<SearchResult> SearchAsync(
        string query,
        int topK = 10,
        bool onlyActive = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return EmptyResult(query);
        }

        topK = Math.Clamp(topK, MinTopProducts, MaxTopProducts);

        var totalSw = Stopwatch.StartNew();

        var retrieval = await RetrieveAsync(query, topK, chunksPerProduct: 1, onlyActive, ct);

        if (retrieval.ProductIds.Count == 0)
        {
            totalSw.Stop();
            return EmptyResult(query, totalSw.ElapsedMilliseconds);
        }

        var cards = await catalog.GetCardsAsync(retrieval.ProductIds, onlyActive, ct);

        var chunksByProduct = retrieval.Chunks
            .GroupBy(c => c.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        var hits = new List<SearchHit>(retrieval.ProductIds.Count);

        foreach (var productId in retrieval.ProductIds)
        {
            if (!cards.TryGetValue(productId, out var card))
            {
                continue;
            }

            chunksByProduct.TryGetValue(productId, out var chunk);

            hits.Add(new SearchHit
            {
                ProductId = card.ProductId,
                Sku = card.Sku,
                Name = card.Name,
                Summary = card.Summary,
                CategoryName = card.CategoryName,
                Tags = card.Tags,
                Price = card.Price,
                StockCount = card.StockCount,
                IsActive = card.IsActive,
                Score = chunk?.Score ?? 0,
                Highlight = chunk?.Text
            });
        }

        totalSw.Stop();

        return new SearchResult
        {
            Query = query,
            Strategy = StrategyName,
            Total = hits.Count,
            TookMs = totalSw.ElapsedMilliseconds,
            Hits = hits
        };
    }

    private async Task<SearchResult> RunLexicalAsync(
        string query,
        int candidates,
        bool onlyActive,
        CancellationToken ct)
    {
        try
        {
            return await lexical.SearchAsync(query, candidates, onlyActive, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lexical search failed for '{Query}'", query);
            return new SearchResult { Query = query, Strategy = "Lexical", Total = 0, TookMs = 0, Hits = [] };
        }
    }

    private async Task<IReadOnlyList<ChunkHit>> RunSemanticAsync(
        string query,
        int chunkLimit,
        bool onlyActive,
        CancellationToken ct)
    {
        try
        {
            return await semantic.SearchChunksAsync(query, chunkLimit, onlyActive, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic search failed for '{Query}'", query);
            return [];
        }
    }

    private static int ResolveCandidateCount(int topProducts) =>
        Math.Clamp(topProducts * CandidateMultiplier, MinCandidates, MaxCandidates);

    private async Task<List<ChunkHit>> LoadLeadChunksAsync(
        List<int> lexicalOnlyProductIds,
        List<int> allLexicalProductIds,
        bool onlyActive,
        CancellationToken ct)
    {
        if (lexicalOnlyProductIds.Count == 0)
        {
            return [];
        }

        var lexicalRankById = allLexicalProductIds
            .Select((id, index) => (Id: id, Rank: index + 1))
            .ToDictionary(x => x.Id, x => x.Rank);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.ProductChunks
            .AsNoTracking()
            .Where(c => lexicalOnlyProductIds.Contains(c.ProductId))
            .Where(c => !onlyActive || c.IsActive)
            .Where(c => c.Ordinal == 0 && LeadChunkTypes.Contains(c.ChunkType))
            .Select(c => new { c.Id, c.ProductId, c.ChunkType, c.Header, c.ChunkText })
            .ToListAsync(ct);

        var chunks = new List<ChunkHit>(rows.Count);

        foreach (var row in rows)
        {
            var rank = lexicalRankById.GetValueOrDefault(row.ProductId, allLexicalProductIds.Count + 1);
            var syntheticScore = 1.0 / (LexicalRankFusionK + rank);

            chunks.Add(new ChunkHit(row.Id, row.ProductId, row.ChunkType, row.Header, row.ChunkText, syntheticScore));
        }

        return chunks;
    }

    private async Task<List<ChunkHit>> ApplyCategorySignalAsync(
        List<ChunkHit> fused,
        string query,
        CancellationToken ct)
    {
        if (fused.Count == 0)
        {
            return fused;
        }

        var index = categoryIndexCache.Current;

        if (index.CategoryCount == 0)
        {
            return fused;
        }

        var detectedCategory = index.DetectCategory(query);

        if (detectedCategory is null)
        {
            return fused;
        }

        logger.LogInformation(
            "Category signal: inferred '{Category}' for query '{Query}'",
            detectedCategory,
            query);

        var productIds = fused
            .Select(c => c.ProductId)
            .Distinct()
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var productCategories = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .ToDictionaryAsync(p => p.Id, p => p.CategoryName, ct);

        var adjusted = new List<ChunkHit>(fused.Count);

        foreach (var chunk in fused)
        {
            var categoryName = productCategories.GetValueOrDefault(chunk.ProductId, string.Empty);

            var isMatch = string.Equals(categoryName, detectedCategory, StringComparison.OrdinalIgnoreCase);

            var adjustment = isMatch ? CategoryMatchBoost : -CategoryMismatchPenalty;

            adjusted.Add(chunk with { Score = chunk.Score + adjustment });
        }

        return adjusted
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    private async Task<List<ChunkHit>> ApplyTagSignalAsync(
        List<ChunkHit> fused,
        string query,
        CancellationToken ct)
    {
        if (fused.Count == 0)
        {
            return fused;
        }

        var productIds = fused
            .Select(c => c.ProductId)
            .Distinct()
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var knownTagNames = await db.Tags
            .AsNoTracking()
            .Select(t => t.Name)
            .ToListAsync(ct);

        if (knownTagNames.Count == 0)
        {
            return fused;
        }

        var knownTags = knownTagNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queryTokens = CategoryTokenIndex.Tokenize(query);

        var queryTags = queryTokens
            .Where(t => knownTags.Contains(t))
            .ToList();

        if (queryTags.Count == 0)
        {
            return fused;
        }

        logger.LogInformation(
            "Tag signal: matched query tags [{Tags}]",
            string.Join(", ", queryTags));

        var productTags = await db.ProductTags
            .AsNoTracking()
            .Where(pt => productIds.Contains(pt.ProductId))
            .Select(pt => new { pt.ProductId, TagName = pt.Tag.Name })
            .ToListAsync(ct);

        var tagsByProduct = productTags
            .GroupBy(pt => pt.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.TagName).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var adjusted = new List<ChunkHit>(fused.Count);

        foreach (var chunk in fused)
        {
            if (!tagsByProduct.TryGetValue(chunk.ProductId, out var tags))
            {
                adjusted.Add(chunk);
                continue;
            }

            var matchedTags = queryTags.Count(qt => tags.Contains(qt));
            var adjustment = matchedTags * TagMatchBoost;

            adjusted.Add(chunk with { Score = chunk.Score + adjustment });
        }

        return adjusted
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    private async Task<List<ChunkHit>> ApplyNameTokenBoostAsync(
        List<ChunkHit> fused,
        string query,
        CancellationToken ct)
    {
        if (fused.Count == 0)
        {
            return fused;
        }

        var queryTokens = CategoryTokenIndex.Tokenize(query);

        if (queryTokens.Count == 0)
        {
            return fused;
        }

        var productIds = fused
            .Select(c => c.ProductId)
            .Distinct()
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var productNames = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var boosted = new List<ChunkHit>(fused.Count);

        foreach (var chunk in fused)
        {
            var name = productNames.GetValueOrDefault(chunk.ProductId, string.Empty);
            var nameTokens = CategoryTokenIndex.Tokenize(name);

            var overlap = 0;

            foreach (var token in nameTokens)
            {
                if (queryTokens.Contains(token))
                {
                    overlap++;
                }
            }

            var adjustment = overlap * NameTokenOverlapBoost;

            boosted.Add(chunk with { Score = chunk.Score + adjustment });
        }

        return boosted
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    private static SearchResult EmptyResult(string query, long tookMs = 0) => new()
    {
        Query = query,
        Strategy = StrategyName,
        Total = 0,
        TookMs = tookMs,
        Hits = []
    };
}