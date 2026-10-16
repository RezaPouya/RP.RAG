using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using RP.Rag.Infrastructure.Elastic.Models;
using RP.RAG.Infrastructure.Elastic.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RP.RAG.Infrastructure.Elastic;

public sealed class ProductElasticIndexer(
    RpRagDbContext db,
    ElasticsearchClientFactory factory,
    ProductIndex index,
    IOptions<ElasticsearchOptions> options,
    ILogger<ProductElasticIndexer> logger)
{
    private const int BatchSize = 250;
    private const int MaxRetries = 3;

    public async Task<IndexResult> RunAsync(bool forceReindex = false, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var client = factory.Create();
        var indexName = options.Value.ProductIndexName;

        if (forceReindex)
        {
            logger.LogInformation("Force reindex requested, deleting index first.");

            await index.DeleteAsync(ct);

            var products = await db.Products.Include(p => p.Sync).ToListAsync(ct);

            foreach (var product in products)
            {
                product.SetToInitialState();
            }

            await db.SaveChangesAsync(ct);
        }

        await index.EnsureCreatedAsync(ct);

        var existingHashes = forceReindex
            ? new Dictionary<int, string>()
            : await LoadHashesAsync(client, indexName, ct);

        logger.LogInformation("Elasticsearch has {Count} existing product hashes.", existingHashes.Count);

        var totalRead = 0;
        var indexed = 0;
        var unchanged = 0;
        var failed = 0;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            var pending = await LoadPendingSyncStatesAsync(ct);

            if (pending.Count == 0)
                break;

            totalRead += pending.Count;

            logger.LogInformation("Processing {Count} products for Elasticsearch.", pending.Count);

            var syncMap = pending.ToDictionary(s => s.ProductId);
            var toIndex = new List<ProductSearchDocument>(pending.Count);
            var alreadySynchronized = new List<int>();

            foreach (var sync in pending)
            {
                var document = ProductSearchDocumentMapper.ToDocument(sync.Product);

                document.SearchTextHash = ComputeHash(document.SearchText);

                if (existingHashes.TryGetValue(sync.ProductId, out var oldHash) && oldHash == document.SearchTextHash)
                {
                    alreadySynchronized.Add(sync.ProductId);
                    continue;
                }

                toIndex.Add(document);
            }

            if (alreadySynchronized.Count > 0)
            {
                var now = DateTime.UtcNow;

                foreach (var productId in alreadySynchronized)
                {
                    var sync = syncMap[productId];

                    sync.ContentChanged = false;
                    sync.ContentSyncedAt = now;
                    sync.ContentSyncRetries = 0;
                    sync.ContentSyncError = null;
                    sync.UpdatedAt = now;

                }

                unchanged += alreadySynchronized.Count;

                await db.SaveChangesAsync(ct);

                logger.LogInformation("Marked {Count} products as already synchronized.", alreadySynchronized.Count);
            }

            if (toIndex.Count == 0)
                continue;

            foreach (var batch in Chunk(toIndex, BatchSize))
            {
                ct.ThrowIfCancellationRequested();

                var result = await BulkIndexAsync(client, indexName, batch, ct);

                var now = DateTime.UtcNow;
                var documentMap = batch.ToDictionary(d => d.ProductId);

                foreach (var productId in result.Successful)
                {
                    if (!syncMap.TryGetValue(productId, out var sync))
                    {
                        logger.LogWarning("ProductSync not found for successfully indexed ProductId={ProductId}.", productId);
                        continue;
                    }

                    sync.ContentChanged = false;
                    sync.ContentSyncedAt = now;

                    sync.EmbeddingChanged = true;

                    sync.ContentSyncRetries = 0;
                    sync.ContentSyncError = null;
                    sync.UpdatedAt = now;

                    if (documentMap.TryGetValue(productId, out var document))
                        existingHashes[productId] = document.SearchTextHash;
                }

                foreach (var failure in result.Failed)
                {
                    if (!syncMap.TryGetValue(failure.Key, out var sync))
                    {
                        logger.LogWarning("ProductSync not found for failed ProductId={ProductId}.", failure.Key);
                        continue;
                    }

                    sync.ContentChanged = true;
                    sync.ContentSyncRetries++;
                    sync.ContentSyncError = Truncate(failure.Value, 500);
                    sync.UpdatedAt = now;
                }

                indexed += result.Successful.Count;
                failed += result.Failed.Count;

                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Progress: indexed {Indexed}, unchanged {Unchanged}, failed {Failed}.",
                    indexed,
                    unchanged,
                    failed);

                if (result.Failed.Count > 0)
                    break;
            }
        }

        sw.Stop();

        var indexResult = new IndexResult
        {
            TotalRead = totalRead,
            Indexed = indexed,
            Unchanged = unchanged,
            Failed = failed,
            DurationSeconds = sw.Elapsed.TotalSeconds,
            IsFullReindex = forceReindex
        };

        logger.LogInformation("Indexing complete in {Seconds:F1}s. Read: {Read}, Indexed: {Indexed}, Unchanged: {Unchanged}, Failed: {Failed}",
            indexResult.DurationSeconds,
            indexResult.TotalRead,
            indexResult.Indexed,
            indexResult.Unchanged,
            indexResult.Failed);

        return indexResult;
    }


    private async Task<List<ProductSync>> LoadPendingSyncStatesAsync(CancellationToken ct)
    {
        return await db.ProductSyncs
            .Include(s => s.Product)
                .ThenInclude(p => p.Category)
            .Include(s => s.Product)
                .ThenInclude(p => p.ProductTags)
                    .ThenInclude(pt => pt.Tag)
            .Include(s => s.Product)
                .ThenInclude(p => p.Documents)
            .Where(s => s.Product.IsActive && (s.ContentChanged && s.ContentSyncRetries < MaxRetries))
            .OrderBy(s => s.ProductId)
            .Take(BatchSize)
            .ToListAsync(ct);
    }

    private async Task<Dictionary<int, string>> LoadHashesAsync(ElasticsearchClient client, string indexName, CancellationToken ct)
    {
        var hashes = new Dictionary<int, string>();
        const int pageSize = 5000;

        IReadOnlyCollection<FieldValue>? searchAfter = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var response = await client.SearchAsync<ProductSearchDocument>(
                s =>
                {
                    s.Indices(indexName)
                        .Size(pageSize)
                        .Sort(sort => sort.Field(f => f.ProductId, so => so.Order(SortOrder.Asc)))
                        .Source(src => src.Filter(f => f.Includes(
                            Infer.Fields<ProductSearchDocument>(p => p.ProductId, p => p.SearchTextHash))));

                    if (searchAfter is not null)
                        s.SearchAfter(searchAfter.ToList());
                },
                ct);

            if (!response.IsValidResponse)
            {
                logger.LogError("Failed to load existing Elasticsearch hashes: {Debug}", response.DebugInformation);
                throw new InvalidOperationException("Failed to load existing Elasticsearch hashes.");
            }

            if (response.Documents.Count == 0)
                break;

            foreach (var document in response.Documents)
                hashes[document.ProductId] = document.SearchTextHash;

            var lastHit = response.Hits.LastOrDefault();

            if (lastHit?.Sort is null || lastHit.Sort.Count == 0)
                break;

            searchAfter = lastHit.Sort;

            if (response.Documents.Count < pageSize)
                break;
        }

        return hashes;
    }

    private async Task<BulkIndexResult> BulkIndexAsync(ElasticsearchClient client, string indexName, IReadOnlyList<ProductSearchDocument> documents, CancellationToken ct)
    {
        if (documents.Count == 0)
            return new BulkIndexResult(new HashSet<int>(), new Dictionary<int, string>());

        var response = await client.BulkAsync(
            b =>
            {
                b.Index(indexName);

                foreach (var document in documents)
                    b.Index(document, operation => operation.Id(document.ProductId.ToString()));
            },
            ct);

        if (!response.IsValidResponse)
        {
            logger.LogError("Elasticsearch bulk index failed: {Debug}", response.DebugInformation);

            return new BulkIndexResult(
                new HashSet<int>(),
                documents.ToDictionary(d => d.ProductId, _ => response.DebugInformation));
        }

        if (response.Items is null)
        {
            logger.LogError("Elasticsearch bulk response did not contain items.");

            return new BulkIndexResult(
                new HashSet<int>(),
                documents.ToDictionary(d => d.ProductId, _ => "Elasticsearch bulk response did not contain items."));
        }

        var successful = new HashSet<int>();
        var failed = new Dictionary<int, string>();

        foreach (var item in response.Items)
        {
            if (item.Error is not null)
            {
                logger.LogError(
                    "Elasticsearch item {Id} failed: {Type} - {Reason}",
                    item.Id,
                    item.Error.Type,
                    item.Error.Reason);

                if (int.TryParse(item.Id, out var productId))
                    failed[productId] = $"{item.Error.Type}: {item.Error.Reason}";

                continue;
            }

            if (int.TryParse(item.Id, out var successfulProductId))
                successful.Add(successfulProductId);
        }

        return new BulkIndexResult(successful, failed);
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            var count = Math.Min(size, source.Count - i);
            var batch = new List<T>(count);

            for (var j = 0; j < count; j++)
                batch.Add(source[i + j]);

            yield return batch;
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max
            ? value
            : value[..max] + "…";
    }

    private sealed record BulkIndexResult(HashSet<int> Successful, Dictionary<int, string> Failed);
}