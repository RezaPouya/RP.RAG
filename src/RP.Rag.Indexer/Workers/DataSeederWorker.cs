using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RP.Rag.Domain.Entities.Products;
using RP.Rag.Infrastructure.Data;
using RP.Rag.Infrastructure.SeedData;

namespace RP.Rag.Indexer.Workers;

public sealed class DataSeederWorker(IServiceScopeFactory scopeFactory, IOptions<DataSeederWorkerOptions> options,
    ILogger<DataSeederWorker> logger) : BackgroundService
{
    private static bool _shouldContinue = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        logger.LogInformation("DataSeederWorker starting. BatchSize={BatchSize}", opts.BatchSize);

        try
        {
            if (!_shouldContinue)
                return;

            await InitializeAsync(opts, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "ProductSync initialization failed.");
            throw;
        }

        logger.LogInformation("DataSeederWorker completed.");
    }

    private async Task InitializeAsync(DataSeederWorkerOptions opts, CancellationToken ct)
    {
        await SeedAsync(ct);

        var totalCreated = 0;

        while (!ct.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();

            var syncedProductIds = await db.ProductSyncs
                .AsNoTracking()
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            var productIds = await db.Products
                .AsNoTracking()
                .Where(p => !syncedProductIds.Contains(p.Id))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .Take(opts.BatchSize)
                .ToListAsync(ct);

            if (productIds.Count == 0)
            {
                _shouldContinue = false;
                break;
            }

            var now = DateTime.UtcNow;

            var syncRows = productIds.Select(productId => new ProductSync
            {
                ProductId = productId,
                ContentChanged = true,
                EmbeddingChanged = false,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            db.ProductSyncs.AddRange(syncRows);
            await db.SaveChangesAsync(ct);

            totalCreated += syncRows.Count;

            logger.LogInformation(
                "Created {Count} ProductSync rows. TotalCreated={TotalCreated}",
                syncRows.Count,
                totalCreated);
        }

        logger.LogInformation("ProductSync initialization finished. TotalCreated={TotalCreated}", totalCreated);
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var seedLoader = scope.ServiceProvider.GetRequiredService<SeedLoader>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        var configured = configuration["Seed:DataRoot"];

        var dataRoot = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "data"));

        try
        {
            await seedLoader.LoadAsync(dataRoot, locale: "", clearExisting: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Seeding from {DataRoot} failed.", dataRoot);
        }
    }
}
