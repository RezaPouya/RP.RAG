using Microsoft.Extensions.Options;
using RP.RAG.Infrastructure.Elastic;

namespace RP.Rag.Indexer.Workers;

public sealed class ProductElasticIndexerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductElasticIndexerWorkerOptions> options,
    ILogger<ProductElasticIndexerWorker> logger) : BackgroundService
{
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

        logger.LogInformation("ProductIndexerWorker starting. PollInterval={PollInterval}s", opts.PollIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opts.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var indexer = scope.ServiceProvider.GetRequiredService<ProductElasticIndexer>();

                var result = await indexer.RunAsync(forceReindex: false, stoppingToken);

                logger.LogInformation(
                    "ProductIndexerWorker iteration completed. Read={Read}, Indexed={Indexed}, Unchanged={Unchanged}, Failed={Failed}",
                    result.TotalRead,
                    result.Indexed,
                    result.Unchanged,
                    result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ProductIndexerWorker iteration failed. Will retry on next tick.");
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

        logger.LogInformation("ProductIndexerWorker stopped.");
    }
}
