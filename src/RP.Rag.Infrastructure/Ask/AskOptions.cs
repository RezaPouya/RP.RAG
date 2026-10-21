namespace RP.RAG.Infrastructure.Ask;

public sealed class AskOptions
{
    public const string SectionName = "Ask";

    public int TopProducts { get; set; } = 5;
    public int ChunksPerProduct { get; set; } = 2;
    public int MaxQuestionLength { get; set; } = 500;
    public bool IncludePartiallyRelevantChunks { get; set; }
    public string NoResultsAnswer { get; set; } =
        "I couldn't find a matching product in the catalog. Try describing what you need in a different way.";
}
