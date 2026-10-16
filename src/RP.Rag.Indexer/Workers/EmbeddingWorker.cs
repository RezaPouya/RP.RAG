using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Embeddings;

namespace RP.Rag.Indexer.Workers;

public sealed class EmbeddingWorker(IServiceScopeFactory scopeFactory, IEmbeddingGenerator embedder, IOptions<EmbeddingWorkerOptions> options, ILogger<EmbeddingWorker> logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private const string EmbeddingVersion = "v1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var opts = options.Value;

        logger.LogInformation(
            "EmbeddingWorker starting. " +
            "Model={Model}, Dims={Dims}, PollInterval={Poll}s, " +
            "BatchSize={Batch}, MaxChunksPerBatch={MaxChunks}, " +
            "MaxRetries={MaxRetries}",
            embedder.ModelName,
            embedder.Dimensions,
            opts.PollIntervalSeconds,
            opts.BatchSize,
            opts.MaxChunksPerBatch,
            MaxRetries);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opts.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Embedding iteration failed. Will retry on next tick.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("EmbeddingWorker stopped.");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();

        var opts = options.Value;

        var pending = await db.ProductSyncs
            .Where(s => s.EmbeddingChanged && s.EmbeddingSyncRetries < MaxRetries)
            .OrderBy(s => s.UpdatedAt)
            .Take(opts.BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        logger.LogInformation("Processing {Count} pending embeddings.", pending.Count);

        var productIds = pending.Select(s => s.ProductId).ToList();

        var syncMap = pending.ToDictionary(s => s.ProductId);

        var products = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
            .Include(p => p.Documents)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var chunkBuilder = new ProductChunkBuilder();

        var chunksByProduct = new Dictionary<int, List<ProductChunk>>();

        foreach (var productId in productIds)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                var sync = syncMap[productId];
                var now = DateTime.UtcNow;

                sync.EmbeddingChanged = false;
                sync.EmbeddingSyncedAt = now;
                sync.EmbeddingSyncRetries = 0;
                sync.EmbeddingSyncError = null;
                sync.UpdatedAt = now;

                continue;
            }

            var chunks = chunkBuilder.Build(product);

            if (chunks.Count > 0)
            {
                chunksByProduct[productId] = chunks;
            }
        }

        var allChunks = chunksByProduct.SelectMany(x => x.Value).ToList();


        if (allChunks.Count > 0)
        {
            try
            {
                var maxChunksPerBatch = Math.Max(1, opts.MaxChunksPerBatch);

                foreach (var chunkBatch in Chunk(allChunks, maxChunksPerBatch))
                {
                    ct.ThrowIfCancellationRequested();

                    var embeddingTexts = chunkBatch
                        .Select(c => c.EmbeddingText)
                        .ToList();

                    var vectors = await GenerateBatchWithRetryAsync(embeddingTexts, ct);

                    if (vectors.Length != chunkBatch.Count)
                    {
                        throw new InvalidOperationException(
                            $"Embedding provider returned " +
                            $"{vectors.Length} vectors for " +
                            $"{chunkBatch.Count} input texts.");
                    }

                    AttachEmbeddings(chunkBatch, vectors);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Embedding generation failed for {Count} chunks.", allChunks.Count);

                await RecordFailureAsync(productIds, ex, ct);

                return;
            }
        }


        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var oldChunks = await db.ProductChunks
                .Where(c => productIds.Contains(c.ProductId))
                .ToListAsync(ct);

            db.ProductChunks.RemoveRange(oldChunks);

            foreach (var chunk in allChunks)
            {
                chunk.Id = 0;

                db.ProductChunks.Add(chunk);
            }

            var now = DateTime.UtcNow;

            foreach (var sync in pending)
            {
                sync.EmbeddingChanged = false;
                sync.EmbeddingSyncedAt = now;
                sync.EmbeddingSyncRetries = 0;
                sync.EmbeddingSyncError = null;
                sync.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            logger.LogInformation("Embedding complete. Products={Products}, Chunks={Chunks}.", pending.Count, allChunks.Count);
        }
        catch (OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);

            logger.LogError(ex, "Failed to persist embeddings.");

            await RecordFailureAsync(productIds, ex, ct);
        }
    }

    private void AttachEmbeddings(IReadOnlyList<ProductChunk> chunks, IReadOnlyList<Vector> vectors)
    {
        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Embedding = vectors[i];
            chunks[i].EmbeddingModel = embedder.ModelName;
            chunks[i].EmbeddingVersion = EmbeddingVersion;
            chunks[i].EmbeddingStatus = "Completed";
        }
    }

    private async Task RecordFailureAsync(IReadOnlyCollection<int> productIds, Exception exception, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();

        var syncs = await db.ProductSyncs.Where(s => productIds.Contains(s.ProductId)).ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var sync in syncs)
        {
            sync.EmbeddingSyncRetries++;

            sync.EmbeddingSyncError = Truncate(exception.Message, 500);

            sync.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Vector[]> GenerateBatchWithRetryAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await embedder.GenerateBatchAsync(texts, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                    throw;

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                logger.LogWarning(
                    ex,
                    "Batch attempt {Attempt}/{Max} failed: " +
                    "{Message}. Retrying in {Delay:F0}s...",
                    attempt,
                    MaxRetries,
                    ex.Message,
                    delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }

        throw new InvalidOperationException("Embedding generation failed unexpectedly.");
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            var count = Math.Min(size, source.Count - i);

            var batch = new List<T>(count);

            for (var j = 0; j < count; j++)
            {
                batch.Add(source[i + j]);
            }

            yield return batch;
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max ? value : value[..max] + "…";
    }
}