using RP.RAG.Application.Catalog;
using RP.RAG.Application.Search.Models;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace RP.RAG.Application.Ask;

public sealed class PromptBuilder
{
    private const string ResourceName = "system-prompt.md";

    public string SystemPrompt { get; }

    public PromptBuilder()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        SystemPrompt = reader.ReadToEnd().Trim();
    }

    public string BuildUserPrompt(
        string question,
        IReadOnlyList<ProductCard> products,
        IReadOnlyList<ChunkHit> chunks)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<context>");

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

            foreach (var chunk in chunks.Where(c => c.ProductId == p.ProductId))
            {
                var label = string.IsNullOrWhiteSpace(chunk.Header) ? chunk.ChunkType : chunk.Header;
                sb.AppendLine($"Excerpt ({label}): {chunk.Text.Trim()}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("</context>");
        sb.AppendLine();
        sb.AppendLine("<question>");
        sb.AppendLine(question);
        sb.AppendLine("</question>");

        return sb.ToString();
    }
}
