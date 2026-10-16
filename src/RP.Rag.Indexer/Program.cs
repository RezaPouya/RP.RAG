using Microsoft.EntityFrameworkCore;
using RP.Rag.Indexer.Workers;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<DataSeederWorkerOptions>(
    builder.Configuration.GetSection(DataSeederWorkerOptions.SectionName));

builder.Services.Configure<ProductElasticIndexerWorkerOptions>(
    builder.Configuration.GetSection(ProductElasticIndexerWorkerOptions.SectionName));

builder.Services.Configure<EmbeddingWorkerOptions>(
    builder.Configuration.GetSection(EmbeddingWorkerOptions.SectionName));

builder.Services.AddHostedService<DataSeederWorker>();
builder.Services.AddHostedService<ProductElasticIndexerWorker>();
builder.Services.AddHostedService<EmbeddingWorker>();

var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        host.Services.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Could not apply database migrations.");
    }
}

host.Run();
