using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.Rag.Application.Ask.Models;
using RP.Rag.Application.Conversations;
using RP.Rag.Application.Conversations.Models;
using RP.Rag.Application.Guardrails;
using RP.Rag.Infrastructure.Ask;
using RP.RAG.Application.Ask;
using RP.RAG.Application.Catalog;
using RP.RAG.Application.Search;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RP.RAG.Infrastructure.Ask;

public sealed class AskService(
    IHybridSearch hybrid,
    IProductCatalogService catalogService,
    IAnswerGeneratorResolver generatorResolver,
    IPromptGuardrail promptGuardrail,
    IConversationMemoryService conversationMemory,
    PromptBuilder prompts,
    IOptions<AskOptions> options,
    ILogger<AskService> logger) : IAskService
{
    private static readonly Regex UrlPattern = new(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AskResponse> AskAsync(
        string userId,
        string question,
        int? topProducts = null,
        Guid? conversationId = null,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var sw = Stopwatch.StartNew();

        question = question.Trim();

        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be empty.", nameof(question));
        }

        if (question.Length > opts.MaxQuestionLength)
        {
            question = question[..opts.MaxQuestionLength];
        }

        ConversationMemory memory;

        if (conversationId is null)
        {
            var created = await conversationMemory.CreateAsync(userId, ct);
            memory = new ConversationMemory(created.ConversationId, created.MessageCount, created.DistilledMemory, []);
        }
        else
        {
            var conversation = await conversationMemory.GetAsync(userId, conversationId.Value, ct)
                ?? throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");

            memory = await conversationMemory.GetMemoryAsync(conversationId.Value, ct)
                ?? throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");
        }

        var activeConversationId = memory.ConversationId;

        // 1. Laya Guardrail Check
        var guardrailResult = await promptGuardrail.EvaluateAsync(question, ct);

        if (!guardrailResult.IsAllowed)
        {
            logger.LogWarning("Question blocked by Laya guardrail: {Reason}", guardrailResult.BlockReason);

            await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.User, question, productIds: null, cancellationToken: ct);
            var blockedCount = await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.Assistant, "I cannot assist with that request.", productIds: null, cancellationToken: ct);

            return new AskResponse
            {
                ConversationId = activeConversationId,
                ConversationCount = blockedCount,
                Question = question,
                Answer = "I cannot assist with that request.",
                IsBlocked = true,
                BlockReason = guardrailResult.BlockReason,
                TookMs = sw.ElapsedMilliseconds,
                Products = [],
                Sources = []
            };
        }

        // 2. Save User Message (FIXED TYPO HERE)
        await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.User, question, productIds: null, cancellationToken: ct);

        // 3. Hybrid Retrieval
        var retrieval = await hybrid.RetrieveAsync(
            question,
            topProducts ?? opts.TopProducts,
            opts.ChunksPerProduct,
            onlyActive: true,
            ct);

        var cards = await catalogService.GetCardsAsync(retrieval.ProductIds, onlyActive: true, ct);

        var products = retrieval.ProductIds
            .Where(cards.ContainsKey)
            .Select(id => cards[id])
            .ToList();

        // 4. Handle No Results
        if (products.Count == 0)
        {
            var noResultsCount = await conversationMemory.AddMessageAsync(
                activeConversationId, ConversationMessageRole.Assistant, opts.NoResultsAnswer, productIds: null, cancellationToken: ct);

            return new AskResponse
            {
                ConversationId = activeConversationId,
                ConversationCount = noResultsCount,
                Question = question,
                Answer = opts.NoResultsAnswer,
                TookMs = sw.ElapsedMilliseconds,
                Products = [],
                Sources = []
            };
        }

        // 5. Filter Chunks & Build Prompt
        var productIds = products.Select(p => p.ProductId).ToList();
        var productIdsSet = productIds.ToHashSet();

        var chunks = retrieval.Chunks
            .Where(c => productIdsSet.Contains(c.ProductId))
            .ToList();

        var userPrompt = prompts.BuildUserPrompt(question, products, chunks, memory);

        // 6. Generate Answer
        IAnswerGenerator generator = generatorResolver.Resolve();
        var raw = await generator.GenerateAsync(prompts.SystemPrompt, userPrompt, ct);
        var answer = UrlPattern.Replace(raw, "").Trim();

        // 7. Save Assistant Message WITH Product IDs for history reconstruction
        var conversationCount = await conversationMemory.AddMessageAsync(
            activeConversationId, ConversationMessageRole.Assistant, answer, productIds: productIds, cancellationToken: ct);

        sw.Stop();

        var refByProduct = products
            .Select((p, i) => (p.ProductId, Ref: i + 1))
            .ToDictionary(x => x.ProductId, x => x.Ref);

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
    }
}