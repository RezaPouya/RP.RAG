using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Embeddings;
using RP.RAG.Application.Search;
using RP.RAG.Application.Search.Models;
using System.Diagnostics;

namespace RP.RAG.Infrastructure.Search;

public sealed class SemanticSearchService(
    IDbContextFactory<RpRagDbContext> dbFactory,
    IEmbeddingGenerator embedder,
    ILogger<SemanticSearchService> logger) : ISemanticSearch
{
    private const string StrategyName = "Semantic";

    private const int MaxChunkLimit = 500;
    private const int MinChunkLimit = 1;
    private const int MaxTopK = 50;
    private const int MinTopK = 1;
    private const int ChunkOverFetchMultiplier = 3;

    public async Task<IReadOnlyList<ChunkHit>> SearchChunksAsync(
        string query,
        int limit = 30,
        bool onlyActive = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        limit = Math.Clamp(limit, MinChunkLimit, MaxChunkLimit);

        var queryVector = await embedder.GenerateAsync(query, ct);
        var modelName = embedder.ModelName;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.ProductChunks
            .AsNoTracking()
            .Where(c => !onlyActive || c.IsActive)
            .Where(c => c.Embedding != null && c.EmbeddingModel == modelName)
            .Select(c => new
            {
                c.Id,
                c.ProductId,
                c.ChunkType,
                c.Header,
                c.ChunkText,
                Distance = c.Embedding!.CosineDistance(queryVector)
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        var hits = new List<ChunkHit>(rows.Count);

        foreach (var row in rows)
        {
            hits.Add(new ChunkHit(
                row.Id,
                row.ProductId,
                row.ChunkType,
                row.Header,
                row.ChunkText,
                DistanceToScore(row.Distance)));
        }

        return hits;
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

        topK = Math.Clamp(topK, MinTopK, MaxTopK);

        var totalSw = Stopwatch.StartNew();
        var embedSw = Stopwatch.StartNew();

        var chunkFetchLimit = topK * ChunkOverFetchMultiplier;

        var chunks = await SearchChunksAsync(query, chunkFetchLimit, onlyActive, ct);

        embedSw.Stop();

        if (chunks.Count == 0)
        {
            totalSw.Stop();
            logger.LogInformation(
                "Semantic search '{Query}': no chunks matched in {Ms}ms",
                query,
                totalSw.ElapsedMilliseconds);

            return ResultWithoutHits(query, totalSw.ElapsedMilliseconds);
        }

        var bestChunks = SelectBestChunkPerProduct(chunks, topK);

        var dbSw = Stopwatch.StartNew();
        var products = await LoadProductsAsync(bestChunks, onlyActive, ct);
        dbSw.Stop();

        var hits = BuildHits(bestChunks, products);

        totalSw.Stop();

        logger.LogInformation(
            "Semantic search '{Query}': {Chunks} chunks, {Hits} hits, embed+search={EmbedMs}ms, db={DbMs}ms, total={TotalMs}ms",
            query,
            chunks.Count,
            hits.Count,
            embedSw.ElapsedMilliseconds,
            dbSw.ElapsedMilliseconds,
            totalSw.ElapsedMilliseconds);

        return new SearchResult
        {
            Query = query,
            Strategy = StrategyName,
            Total = hits.Count,
            TookMs = totalSw.ElapsedMilliseconds,
            Hits = hits
        };
    }

    private static List<ChunkHit> SelectBestChunkPerProduct(
        IReadOnlyList<ChunkHit> chunks,
        int topK)
    {
        var bestByProduct = new Dictionary<int, ChunkHit>(chunks.Count);
        var insertionOrder = new List<int>(chunks.Count);

        foreach (var chunk in chunks)
        {
            if (!bestByProduct.TryGetValue(chunk.ProductId, out var existing))
            {
                bestByProduct[chunk.ProductId] = chunk;
                insertionOrder.Add(chunk.ProductId);
                continue;
            }

            if (chunk.Score > existing.Score)
            {
                bestByProduct[chunk.ProductId] = chunk;
            }
        }

        var result = new List<ChunkHit>(Math.Min(topK, bestByProduct.Count));

        foreach (var productId in insertionOrder)
        {
            if (result.Count >= topK)
            {
                break;
            }

            result.Add(bestByProduct[productId]);
        }

        return result;
    }

    private async Task<Dictionary<int, Product>> LoadProductsAsync(
        IReadOnlyList<ChunkHit> chunks,
        bool onlyActive,
        CancellationToken ct)
    {
        var productIds = chunks
            .Select(c => c.ProductId)
            .Distinct()
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => productIds.Contains(p.Id))
            .Where(p => !onlyActive || p.IsActive)
            .ToDictionaryAsync(p => p.Id, ct);
    }

    private static List<SearchHit> BuildHits(
        IReadOnlyList<ChunkHit> chunks,
        Dictionary<int, Product> products)
    {
        var hits = new List<SearchHit>(chunks.Count);

        foreach (var chunk in chunks)
        {
            if (!products.TryGetValue(chunk.ProductId, out var product))
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
                Score = chunk.Score,
                Highlight = chunk.Text
            });
        }

        return hits;
    }

    private static double DistanceToScore(double distance)
    {
        if (double.IsNaN(distance) || double.IsInfinity(distance))
        {
            return 0;
        }

        return Math.Exp(-distance);
    }

    private static SearchResult EmptyResult(string query) => new()
    {
        Query = query,
        Strategy = StrategyName,
        Total = 0,
        TookMs = 0,
        Hits = []
    };

    private static SearchResult ResultWithoutHits(string query, long tookMs) => new()
    {
        Query = query,
        Strategy = StrategyName,
        Total = 0,
        TookMs = tookMs,
        Hits = []
    };
}