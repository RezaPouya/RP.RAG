namespace RP.Rag.Indexer.Workers;

public sealed class ProductElasticIndexerWorkerOptions
{
    public const string SectionName = "ProductIndexerWorker";

    public int PollIntervalSeconds { get; set; } = 30;
}