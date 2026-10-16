using Microsoft.EntityFrameworkCore;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Infrastructure;
using RP.RAG.Infrastructure.Elastic;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not apply database migrations.");
    }

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

try
{
    using var scope = app.Services.CreateScope();
    var index = scope.ServiceProvider.GetRequiredService<ProductIndex>();
    await index.EnsureCreatedAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not ensure the Elasticsearch index on startup.");
}

app.Run();
