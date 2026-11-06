namespace RP.Rag.Application.Ask.Models;

public sealed class AskSource
{
    public int ProductRef { get; init; }
    public int ProductId { get; init; }
    public string ChunkType { get; init; } = "";
    public string? Header { get; init; }
}
