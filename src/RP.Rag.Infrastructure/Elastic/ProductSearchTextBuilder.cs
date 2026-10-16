using System.Text;

namespace RP.RAG.Infrastructure.Elastic;

public static class ProductSearchTextBuilder
{
    public static string Build(
        string? sku,
        string? name,
        string? summary,
        string? description,
        string? categoryName,
        IEnumerable<string>? tags)
    {
        var sb = new StringBuilder();

        Append(sb, "SKU", sku);
        Append(sb, "Name", name);
        Append(sb, "Category", categoryName);
        Append(sb, "Summary", summary);
        Append(sb, "Description", description);

        if (tags is not null)
        {
            var joined = string.Join(", ", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
            Append(sb, "Tags", joined);
        }

        return sb.ToString().Trim();
    }

    private static void Append(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        sb.Append(label).Append(": ").AppendLine(value.Trim());
    }
}