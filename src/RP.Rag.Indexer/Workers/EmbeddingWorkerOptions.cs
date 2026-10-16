namespace RP.Rag.Indexer.Workers;

public sealed class EmbeddingWorkerOptions
{
    public const string SectionName = "EmbeddingWorker";

    public int PollIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 200;

    public int MaxChunksPerBatch { get; set; } = 2000;
}