using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RP.RAG.Application.Embeddings;
using System.Diagnostics;

namespace RP.RAG.Infrastructure.Search;

public sealed class OllamaWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OllamaWarmupHostedService> logger) : IHostedService
{
    private const string WarmupText = "warmup query for embedding model initialization";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator>();

            var sw = Stopwatch.StartNew();
            await embedder.GenerateAsync(WarmupText, cancellationToken);
            sw.Stop();

            logger.LogInformation(
                "Embedding model '{Model}' warmed up in {Ms}ms.",
                embedder.ModelName,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embedding warmup failed. First query may be slow.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}