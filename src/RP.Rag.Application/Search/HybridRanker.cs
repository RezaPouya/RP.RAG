using RP.RAG.Application.Search.Models;

namespace RP.RAG.Application.Search;

public static class HybridRanker
{
    public const int DefaultK = 60;

    public static double Score(int rank, int k = DefaultK) => 1.0 / (k + rank);

    public static List<ChunkHit> Fuse(
        IReadOnlyList<ChunkHit> semanticChunks,
        IReadOnlyList<int> lexicalIds,
        IReadOnlyList<ChunkHit> lexicalOnlyChunks,
        int k = DefaultK)
    {
        var lexicalRank = new Dictionary<int, int>();

        for (var i = 0; i < lexicalIds.Count; i++)
        {
            lexicalRank.TryAdd(lexicalIds[i], i + 1);
        }

        var fused = new Dictionary<long, ChunkHit>();

        for (var i = 0; i < semanticChunks.Count; i++)
        {
            var chunk = semanticChunks[i];

            var score = Score(i + 1, k);

            if (lexicalRank.TryGetValue(chunk.ProductId, out var rank))
            {
                score += Score(rank, k);
            }

            fused.TryAdd(chunk.ChunkId, chunk with { Score = score });
        }

        foreach (var chunk in lexicalOnlyChunks)
        {
            if (fused.ContainsKey(chunk.ChunkId))
            {
                continue;
            }

            if (!lexicalRank.TryGetValue(chunk.ProductId, out var rank))
            {
                continue;
            }

            fused[chunk.ChunkId] = chunk with { Score = Score(rank, k) };
        }

        return fused.Values
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.ChunkId)
            .ToList();
    }
}