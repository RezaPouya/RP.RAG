using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RP.Rag.Infrastructure.Data;
using RP.Rag.Infrastructure.Search;
using RP.Rag.Infrastructure.SeedData;
using RP.RAG.Application.Ask;
using RP.RAG.Application.Catalog;
using RP.RAG.Application.Embeddings;
using RP.RAG.Application.Search;
using RP.RAG.Infrastructure.Ask;
using RP.RAG.Infrastructure.Catalog;
using RP.RAG.Infrastructure.Data.Interceptors;
using RP.RAG.Infrastructure.Elastic;
using RP.RAG.Infrastructure.Embeddings;
using RP.RAG.Infrastructure.Search;

namespace RP.RAG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddSingleton<ProductSyncInterceptor>();

        services.AddDbContextFactory<RpRagDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<ProductSyncInterceptor>());

            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.UseVector();
                    npgsql.MigrationsAssembly(typeof(RpRagDbContext).Assembly.FullName);
                });
        });

        services.AddScoped<RpRagDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<RpRagDbContext>>().CreateDbContext());

        services.AddOptions<ElasticsearchOptions>()
            .BindConfiguration(ElasticsearchOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<ElasticsearchClientFactory>();
        services.AddSingleton<ProductIndex>();

        services.AddScoped<ProductElasticIndexer>();
        services.AddScoped<ILexicalSearch, LexicalSearchService>();
        services.AddScoped<ISemanticSearch, SemanticSearchService>();
        services.AddScoped<IHybridSearch, HybridSearchService>();
        services.AddScoped<IProductCatalogService, ProductCatalogService>();

        // Laya input guardrail
        services.AddOptions<LayaOptions>()
            .BindConfiguration(LayaOptions.SectionName)
            .Validate(o => Uri.TryCreate(o.Url, UriKind.Absolute, out _), "Laya:Url must be an absolute URL.")
            .Validate(o => o.RequestTimeoutSeconds > 0, "Laya:RequestTimeoutSeconds must be positive.")
            .Validate(o => o.JailbreakThreshold is >= 0 and <= 1, "Laya:JailbreakThreshold must be between 0 and 1.")
            .Validate(o => o.PromptInjectionThreshold is >= 0 and <= 1, "Laya:PromptInjectionThreshold must be between 0 and 1.")
            .Validate(o => o.ChunkPromptInjectionThreshold is >= 0 and <= 1, "Laya:ChunkPromptInjectionThreshold must be between 0 and 1.")
            .Validate(o => o.FinalJailbreakThreshold is >= 0 and <= 1, "Laya:FinalJailbreakThreshold must be between 0 and 1.")
            .Validate(o => o.FinalPromptInjectionThreshold is >= 0 and <= 1, "Laya:FinalPromptInjectionThreshold must be between 0 and 1.")
            .ValidateOnStart();

        services.AddHttpClient(ServiceKeys.Laya, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<LayaOptions>>().Value;
            http.BaseAddress = new Uri(opts.Url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        });

        services.AddScoped<IPromptGuardrail, LayaPromptGuardrail>();
        services.AddScoped<IChunkRelevanceEvaluator, LayaChunkRelevanceEvaluator>();
        services.AddScoped<IFinalPromptGuardrail, LayaFinalPromptGuardrail>();

        services.AddSingleton<CategoryTokenIndexCache>();
        services.AddScoped<CategoryTokenIndexBuilder>();
        services.AddHostedService<CategoryTokenIndexWarmupService>();

        // Ollama
        services.AddOptions<OllamaOptions>()
            .BindConfiguration(OllamaOptions.SectionName)
            .ValidateOnStart();

        services.AddHttpClient(ServiceKeys.Ollama, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            http.BaseAddress = new Uri(opts.Url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
        });

        services.AddScoped<IEmbeddingGenerator, OllamaEmbeddingGenerator>();

        // DeepSeek
        services.AddOptions<DeepSeekOptions>()
            .BindConfiguration(DeepSeekOptions.SectionName)
            .ValidateOnStart();

        services.AddHttpClient(ServiceKeys.DeepSeek, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
            http.BaseAddress = new Uri(opts.Url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
        });

        // Keyed answer generators
        services.AddKeyedScoped<IAnswerGenerator, OllamaAnswerGenerator>(ServiceKeys.Ollama);
        services.AddKeyedScoped<IAnswerGenerator, DeepSeekAnswerGenerator>(ServiceKeys.DeepSeek);

        // Generation provider selection
        services.AddOptions<GenerationOptions>()
            .BindConfiguration(GenerationOptions.SectionName);

        services.AddScoped<IAnswerGeneratorResolver, AnswerGeneratorResolver>();

        services.AddOptions<AskOptions>()
            .BindConfiguration(AskOptions.SectionName);

        services.AddSingleton<PromptBuilder>();
        services.AddScoped<IAskService, AskService>();

        services.AddScoped<SeedLoader>();

        services.AddHostedService<OllamaWarmupHostedService>();

        return services;
    }
}
