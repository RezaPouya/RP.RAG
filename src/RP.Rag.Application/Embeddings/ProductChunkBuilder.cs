using RP.Rag.Domain.Entities.Products;
using System.Security.Cryptography;
using System.Text;

namespace RP.RAG.Application.Embeddings;

public sealed class ProductChunkBuilder
{
    private const int MaxChunkChars = 800;
    private const int MinChunkChars = 50;
    private const int MaxNamePrefixChars = 80;

    public List<ProductChunk> Build(Product product)
    {
        var chunks = new List<ProductChunk>();
        var namePrefix = BuildNamePrefix(product);

        AddSummary(chunks, product);

        AddSection(chunks, product, namePrefix,
            chunkType: "description",
            header: "Description",
            content: product.Description,
            documentId: null);

        foreach (var doc in product.Documents.Where(d => d.IsActive))
        {
            AddSection(chunks, product, namePrefix,
                chunkType: "document",
                header: doc.Title,
                content: doc.Content,
                documentId: doc.Id);
        }

        return chunks;
    }

    private static void AddSummary(List<ProductChunk> chunks, Product p)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(p.Name)) parts.Add(p.Name);
        if (!string.IsNullOrWhiteSpace(p.Summary)) parts.Add(p.Summary);

        var cats = p.Category?.Name;
        if (!string.IsNullOrWhiteSpace(cats)) parts.Add(cats);

        if (parts.Count == 0) return;

        var text = string.Join(" | ", parts);

        chunks.Add(new ProductChunk
        {
            ProductId = p.Id,
            ChunkType = "summary",
            Ordinal = 0,
            Header = "Summary",
            ChunkText = text,
            EmbeddingText = text,
            ChunkTextHash = Hash(text),
            IsActive = p.IsActive,
            EmbeddingStatus = "Pending"
        });
    }

    private static void AddSection(
        List<ProductChunk> chunks,
        Product p,
        string namePrefix,
        string chunkType,
        string header,
        string? content,
        int? documentId)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var text = content.Trim();

        if (text.Length <= MaxChunkChars)
        {
            AddChunk(chunks, p, namePrefix, chunkType, header,
                documentId, 0, text);
            return;
        }

        var paragraphs = text.Split(
            new[] { "\n\n", "\r\n\r\n" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var ordinal = 0;
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length < MinChunkChars) { buffer.Clear(); return; }

            AddChunk(chunks, p, namePrefix, chunkType, header,
                documentId, ordinal++, buffer.ToString().Trim());

            buffer.Clear();
        }

        foreach (var para in paragraphs)
        {
            if (buffer.Length > 0 && buffer.Length + para.Length + 2 > MaxChunkChars)
                Flush();

            if (buffer.Length > 0) buffer.Append("\n\n");
            buffer.Append(para);
        }

        Flush();
    }

    private static void AddChunk(
        List<ProductChunk> chunks,
        Product p,
        string namePrefix,
        string chunkType,
        string header,
        int? documentId,
        int ordinal,
        string text)
    {
        var embeddingText = string.IsNullOrWhiteSpace(namePrefix)
            ? $"{header}\n{text}"
            : $"{header} — {namePrefix}\n{text}";

        chunks.Add(new ProductChunk
        {
            ProductId = p.Id,
            DocumentId = documentId,
            ChunkType = chunkType,
            Ordinal = ordinal,
            Header = header,
            ChunkText = text,
            EmbeddingText = embeddingText,
            ChunkTextHash = Hash(text),
            IsActive = p.IsActive,
            EmbeddingStatus = "Pending"
        });
    }

    private static string BuildNamePrefix(Product p)
    {
        var parts = new[] { p.Name, p.Category?.Name }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var s = string.Join(" ", parts);

        return TruncateSafe(s, MaxNamePrefixChars);
    }

    // Avoids cutting a UTF-16 surrogate pair in half.
    private static string TruncateSafe(string s, int maxChars)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxChars) return s;

        var end = maxChars;

        if (char.IsHighSurrogate(s[end - 1]))
            end--;

        return s[..end];
    }

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}