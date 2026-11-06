using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.Rag.Application.Ask.Models;
using RP.Rag.Application.Conversations;
using RP.Rag.Application.Conversations.Models;
using RP.Rag.Application.Guardrails;
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
    IPromptGuardrail promptGuardrail, // Replaced IChunkRelevanceEvaluator
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
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be empty.", nameof(question));
        }

        if (question.Length > opts.MaxQuestionLength)
        {
            question = question[..opts.MaxQuestionLength];
        }

        // 1. Load or Create Conversation
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

        // 2. Laya Check 1: Incoming Question ONLY (Fast & Single Call)
        var guardrailResult = await promptGuardrail.EvaluateAsync(question, ct);
        if (!guardrailResult.IsAllowed)
        {
            logger.LogWarning("Question blocked by Laya guardrail: {Reason}", guardrailResult.BlockReason);

            // Save the blocked interaction to keep conversation history intact
            await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.User, question, ct);
            var blockedCount = await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.Assistant, "I cannot assist with that request.", ct);

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

        // 3. Save User Message
        await conversationMemory.AddMessageAsync(activeConversationId, ConversationMessageRole.User, question, ct);

        // 4. Retrieve Catalog Context (Vector/Hybrid Search)
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

        // 5. Handle No Results
        if (products.Count == 0)
        {
            var noResultsCount = await conversationMemory.AddMessageAsync(
                activeConversationId, ConversationMessageRole.Assistant, opts.NoResultsAnswer, ct);

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

        // 6. Filter Chunks (No Laya chunk evaluation, just keep retrieved context)
        var productIds = products.Select(p => p.ProductId).ToHashSet();
        var chunks = retrieval.Chunks
            .Where(c => productIds.Contains(c.ProductId))
            .ToList();

        // 7. Build Prompt (Uses the new 4-phase PromptBuilder)
        var userPrompt = prompts.BuildUserPrompt(question, products, chunks, memory);

        // 8. Generate Answer
        IAnswerGenerator generator = generatorResolver.Resolve();
        var raw = await generator.GenerateAsync(prompts.SystemPrompt, userPrompt, ct);
        var answer = UrlPattern.Replace(raw, "").Trim();

        // 9. Save Assistant Message (NO synchronous distillation)
        var conversationCount = await conversationMemory.AddMessageAsync(
            activeConversationId, ConversationMessageRole.Assistant, answer, ct);

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