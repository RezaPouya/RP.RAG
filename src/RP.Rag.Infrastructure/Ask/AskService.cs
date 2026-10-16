using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.RAG.Application.Ask;
using RP.RAG.Application.Catalog;
using RP.RAG.Application.Search;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RP.RAG.Infrastructure.Ask;

public sealed class AskService(
    IHybridSearch hybrid,
    IProductCatalogService catalog,
    IAnswerGeneratorResolver generatorResolver,
    PromptBuilder prompts,
    IOptions<AskOptions> options,
    ILogger<AskService> logger) : IAskService
{
    private static readonly Regex UrlPattern = new(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AskResponse> AskAsync(string question, int? topProducts = null, CancellationToken ct = default)
    {
        var opts = options.Value;
        var sw = Stopwatch.StartNew();

        question = question.Trim();

        if (question.Length > opts.MaxQuestionLength)
            question = question[..opts.MaxQuestionLength];

        var retrieval = await hybrid.RetrieveAsync(
            question,
            topProducts ?? opts.TopProducts,
            opts.ChunksPerProduct,
            onlyActive: true,
            ct);

        var cards = await catalog.GetCardsAsync(retrieval.ProductIds, onlyActive: true, ct);

        var products = retrieval.ProductIds
            .Where(cards.ContainsKey)
            .Select(id => cards[id])
            .ToList();

        if (products.Count == 0)
        {
            return new AskResponse
            {
                Question = question,
                Answer = opts.NoResultsAnswer,
                TookMs = sw.ElapsedMilliseconds
            };
        }

        var productIds = products.Select(p => p.ProductId).ToHashSet();
        var chunks = retrieval.Chunks.Where(c => productIds.Contains(c.ProductId)).ToList();
        var userPrompt = prompts.BuildUserPrompt(question, products, chunks);

        IAnswerGenerator generator = generatorResolver.Resolve();

        var raw = await generator.GenerateAsync(prompts.SystemPrompt, userPrompt, ct);

        var answer = UrlPattern.Replace(raw, "").Trim();

        var refByProduct = products
            .Select((p, i) => (p.ProductId, Ref: i + 1))
            .ToDictionary(x => x.ProductId, x => x.Ref);

        sw.Stop();

        logger.LogInformation(
            "Ask '{Question}': {Products} products, {Chunks} chunks, {Ms}ms, model={Model}",
            question, products.Count, chunks.Count, sw.ElapsedMilliseconds, generator.ModelName);

        return new AskResponse
        {
            Question = question,
            Answer = answer,
            Model = generator.ModelName,
            TookMs = sw.ElapsedMilliseconds,
            Products = products,
            Sources = chunks
                .Select(c => new AskSource
                {
                    ProductRef = refByProduct[c.ProductId],
                    ProductId = c.ProductId,
                    ChunkType = c.ChunkType,
                    Header = c.Header
                })
                .ToList()
        };
    }
}