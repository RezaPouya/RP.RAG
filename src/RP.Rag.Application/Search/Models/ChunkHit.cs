namespace RP.RAG.Application.Search.Models;

public sealed record ChunkHit(
    long ChunkId,
    int ProductId,
    string ChunkType,
    string? Header,
    string Text,
    double Score);
