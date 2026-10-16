namespace RP.Rag.Infrastructure.Elastic.Models;

public sealed class IndexResult
{
    public int TotalRead { get; init; }
    public int Indexed { get; init; }
    public int Unchanged { get; init; }
    public int Failed { get; init; }
    public double DurationSeconds { get; init; }
    public bool IsFullReindex { get; init; }
}