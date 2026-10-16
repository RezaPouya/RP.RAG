namespace RP.Rag.Indexer.Workers;

public sealed class DataSeederWorkerOptions
{
    public const string SectionName = "DataSeederWorkerOptions";

    public int BatchSize { get; set; } = 500;
}
