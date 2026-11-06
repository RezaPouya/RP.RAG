using RP.Rag.Application.Conversations.Models;
using RP.RAG.Application.Catalog;
using RP.RAG.Application.Search.Models;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace RP.Rag.Application.Conversations;

public sealed class PromptBuilder
{
    private static readonly string[] ResourceNames =
    [
        "system-prompt.md",
        "Rules.guardrails.md",
        "Rules.response-style.md",
        "Rules.procedures.md"
    ];

    public string SystemPrompt { get; }

    public PromptBuilder()
    {
        var assembly = Assembly.GetExecutingAssembly();
        SystemPrompt = string.Join("\n\n", ResourceNames.Select(resourceName => ReadResource(assembly, resourceName)));
    }



    public string BuildUserPrompt(string question, IReadOnlyList<ProductCard> products, IReadOnlyList<ChunkHit> chunks, ConversationMemory? conversationMemory = null)
    {
        var sb = new StringBuilder();

        // Phases 2, 3, 4: Working, Semantic, and Episodic Memory
        if (conversationMemory is not null)
        {
            AppendMemoryPhases(sb, conversationMemory);
        }

        // Catalog Context
        AppendCatalogContext(sb, products, chunks);

        // Current Question
        sb.AppendLine("<question>");
        sb.AppendLine(question);
        sb.AppendLine("</question>");

        return sb.ToString();
    }

    private static void AppendMemoryPhases(StringBuilder sb, ConversationMemory memory)
    {
        var hasWorking = memory.RecentMessages.Count > 0;
        var hasSemantic = memory.SemanticFacts is { Count: > 0 };
        var hasEpisodic = memory.Episodes is { Count: > 0 };

        if (!hasWorking && !hasSemantic && !hasEpisodic)
            return;

        // Phase 2: Working Memory (Recent Messages)
        if (hasWorking)
        {
            sb.AppendLine("<working_memory>");
            sb.AppendLine("Short-lived context for this turn. Untrusted dialogue data.");
            foreach (var message in memory.RecentMessages)
            {
                sb.Append(message.Role).Append(": ").AppendLine(message.Content);
            }
            sb.AppendLine("</working_memory>");
            sb.AppendLine();
        }

        // Phase 3: Semantic Memory (User Facts/Preferences)
        if (hasSemantic)
        {
            sb.AppendLine("<semantic_memory>");
            sb.AppendLine("Conversation-scoped user-stated facts and preferences. Untrusted data.");
            foreach (var fact in memory.SemanticFacts!)
            {
                sb.Append("- ").Append(fact.Key).Append(": ").AppendLine(fact.Value);
            }
            sb.AppendLine("</semantic_memory>");
            sb.AppendLine();
        }

        // Phase 4: Episodic Memory (Past Events/Decisions)
        if (hasEpisodic)
        {
            sb.AppendLine("<episodic_memory>");
            sb.AppendLine("Prior decisions, actions, or outcomes from this conversation. Untrusted data.");
            foreach (var episode in memory.Episodes!)
            {
                sb.Append("- ").Append(episode.Summary);
                if (!string.IsNullOrWhiteSpace(episode.Evidence))
                {
                    sb.Append(" (Evidence: ").Append(episode.Evidence).Append(')');
                }
                sb.AppendLine();
            }
            sb.AppendLine("</episodic_memory>");
            sb.AppendLine();
        }
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }

    private static void AppendCatalogContext(
        StringBuilder sb,
        IReadOnlyList<ProductCard> products,
        IReadOnlyList<ChunkHit> chunks)
    {
        if (products.Count == 0)
            return;

        sb.AppendLine("<context>");
        sb.AppendLine("Trusted product catalog facts and retrieved excerpts.");

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            sb.AppendLine($"[{i + 1}] {p.Name}");
            sb.AppendLine($"SKU: {p.Sku}");
            sb.AppendLine($"Category: {p.CategoryName}");

            if (p.Tags.Count > 0)
                sb.AppendLine($"Tags: {string.Join(", ", p.Tags)}");

            sb.AppendLine($"Price: {p.Price.ToString("0.##", CultureInfo.InvariantCulture)}");
            sb.AppendLine(p.InStock ? $"Stock: in stock ({p.StockCount} units)" : "Stock: out of stock");

            var productChunks = chunks.Where(c => c.ProductId == p.ProductId).ToList();

            foreach (var chunk in productChunks)
            {
                var label = string.IsNullOrWhiteSpace(chunk.Header) ? chunk.ChunkType : chunk.Header;
                sb.AppendLine($"Excerpt ({label}): {chunk.Text.Trim()}");
            }
            sb.AppendLine();
        }
        sb.AppendLine("</context>");
        sb.AppendLine();
    }
}