using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RP.RAG.Infrastructure.Elastic;

namespace RP.RAG.Infrastructure.Search;

public sealed class CategoryTokenIndexWarmupService(
    IServiceScopeFactory scopeFactory,
    CategoryTokenIndexCache cache,
    ILogger<CategoryTokenIndexWarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var builder = scope.ServiceProvider.GetRequiredService<CategoryTokenIndexBuilder>();

            var index = await builder.BuildAsync(cancellationToken);
            cache.Set(index);

            logger.LogInformation(
                "Category token index warmup complete: {Categories} categories.",
                index.CategoryCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Category token index warmup failed. Hybrid search will run without category signal.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}