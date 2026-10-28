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
    IFinalPromptGuardrail finalPromptGuardrail,
    IConversationMemoryService conversationMemory,
    PromptBuilder prompts,
    IOptions<AskOptions> options,
    ILogger<AskService> logger) : IAskService
{
    private static readonly Regex UrlPattern = new(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AskResponse> AskAsync(
        string question,
        int? topProducts = null,
        Guid? conversationId = null,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var sw = Stopwatch.StartNew();

        question = question.Trim();

        ConversationMemory memory;
        if (conversationId is null)
        {
            var created = await conversationMemory.CreateAsync(ct);
            memory = new ConversationMemory(created.ConversationId, created.MessageCount, created.DistilledMemory, []);
        }
        else
        {
            memory = await conversationMemory.GetMemoryAsync(conversationId.Value, ct)
                ?? throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");
        }
        var activeConversationId = memory.ConversationId;

        var guardrail = await promptGuardrail.EvaluateAsync(question, ct);
        if (question.Length > opts.MaxQuestionLength)
            question = question[..opts.MaxQuestionLength];

        if (!guardrail.IsAllowed)
        {
            await conversationMemory.AddMessageAsync(
                activeConversationId, ConversationMessageRole.User, question, ct);
            var blockedCount = await SaveTurnAsync("I can't help with that request.");
            logger.LogWarning(
                "Blocked incoming prompt. Jailbreak={JailbreakProbability}, PromptInjection={PromptInjectionProbability}",
                guardrail.JailbreakProbability,
                guardrail.PromptInjectionProbability);

            return new AskResponse
            {
                ConversationId = activeConversationId,
                ConversationCount = blockedCount,
                Question = question,
                Answer = "I can't help with that request.",
                IsBlocked = true,
                BlockReason = guardrail.BlockReason,
                TookMs = sw.ElapsedMilliseconds
            };
        }

        await conversationMemory.AddMessageAsync(
            activeConversationId, ConversationMessageRole.User, question, ct);

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
            var noResultsCount = await SaveTurnAsync(opts.NoResultsAnswer);
            return new AskResponse
            {
                ConversationId = activeConversationId,
                ConversationCount = noResultsCount,
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
                (result.Relevance == ChunkRelevance.Relevant ||
                    (opts.IncludePartiallyRelevantChunks && result.Relevance == ChunkRelevance.PartiallyRelevant)) &&
                !result.ContainsPromptInjection)
            .ToList();

        var partiallyRelevantCount = relevanceResults.Count(result => result.Relevance == ChunkRelevance.PartiallyRelevant);
        var irrelevantCount = relevanceResults.Count(result => result.Relevance == ChunkRelevance.Irrelevant);
        var injectionFlaggedCount = relevanceResults.Count(result => result.ContainsPromptInjection);

        logger.LogInformation(
            "Stage 2 retained {KeptChunks} of {RetrievedChunks} chunks; partial={PartialChunks}, irrelevant={IrrelevantChunks}, injection={InjectionChunks}",
            chunks.Count,
            retrievedChunks.Count,
            partiallyRelevantCount,
            irrelevantCount,
            injectionFlaggedCount);

        var userPrompt = prompts.BuildUserPrompt(question, products, chunks, memory);

        var finalAssessment = await finalPromptGuardrail.EvaluateAsync(prompts.SystemPrompt, userPrompt, ct);
        if (!finalAssessment.IsAllowed)
        {
            const string blockedAnswer = "I can't safely use the assembled request to answer this question.";
            var blockedCount = await SaveTurnAsync(blockedAnswer);
            logger.LogWarning(
                "Blocked assembled request. Jailbreak={JailbreakProbability}, PromptInjection={PromptInjectionProbability}",
                finalAssessment.JailbreakProbability,
                finalAssessment.PromptInjectionProbability);

            return new AskResponse
            {
                ConversationId = activeConversationId,
                ConversationCount = blockedCount,
                Question = question,
                Answer = blockedAnswer,
                IsBlocked = true,
                BlockReason = finalAssessment.BlockReason,
                TookMs = sw.ElapsedMilliseconds
            };
        }

        IAnswerGenerator generator = generatorResolver.Resolve();

        var raw = await generator.GenerateAsync(prompts.SystemPrompt, userPrompt, ct);

        var answer = UrlPattern.Replace(raw, "").Trim();
        var conversationCount = await SaveTurnAsync(answer);

        var refByProduct = products
            .Select((p, i) => (p.ProductId, Ref: i + 1))
            .ToDictionary(x => x.ProductId, x => x.Ref);

        sw.Stop();

        logger.LogInformation(
            "Ask '{Question}': {Products} products, {Chunks} chunks, {Ms}ms, model={Model}",
            question, products.Count, chunks.Count, sw.ElapsedMilliseconds, generator.ModelName);

        return new AskResponse
        {
            ConversationId = activeConversationId,
            ConversationCount = conversationCount,
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

        async Task<int> SaveTurnAsync(string answerText)
        {
            var count = await conversationMemory.AddMessageAsync(
                activeConversationId, ConversationMessageRole.Assistant, answerText, ct);
            await conversationMemory.DistillIfNeededAsync(activeConversationId, ct);
            return count;
        }
    }
}
