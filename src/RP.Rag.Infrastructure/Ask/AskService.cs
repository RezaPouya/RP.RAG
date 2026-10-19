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
    IPromptGuardrail promptGuardrail,
    IChunkRelevanceEvaluator chunkRelevanceEvaluator,
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

        var guardrail = await promptGuardrail.EvaluateAsync(question, ct);
        if (!guardrail.IsAllowed)
        {
            logger.LogWarning(
                "Blocked incoming prompt. Jailbreak={JailbreakProbability}, PromptInjection={PromptInjectionProbability}",
                guardrail.JailbreakProbability,
                guardrail.PromptInjectionProbability);

            return new AskResponse
            {
                Question = question,
                Answer = "I can't help with that request.",
                IsBlocked = true,
                BlockReason = guardrail.BlockReason,
                TookMs = sw.ElapsedMilliseconds
            };
        }

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
        var retrievedChunks = retrieval.Chunks.Where(c => productIds.Contains(c.ProductId)).ToList();
        var relevanceResults = await chunkRelevanceEvaluator.EvaluateAsync(question, retrievedChunks, ct);
        var relevanceByChunk = relevanceResults.ToDictionary(result => result.ChunkId);
        var chunks = retrievedChunks
            .Where(chunk => relevanceByChunk.TryGetValue(chunk.ChunkId, out var result) &&
                result.Relevance != ChunkRelevance.Irrelevant &&
                !result.ContainsPromptInjection)
            .ToList();

        logger.LogInformation(
            "Stage 2 retained {KeptChunks} of {RetrievedChunks} retrieved chunks for question '{Question}'",
            chunks.Count,
            retrievedChunks.Count,
            question);

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
