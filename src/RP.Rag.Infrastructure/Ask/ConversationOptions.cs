namespace RP.RAG.Infrastructure.Ask;

public sealed class ConversationOptions
{
    public const string SectionName = "ConversationMemory";

    public int RecentMessages { get; set; } = 6;
    /// <summary>Minimum number of unsummarized messages older than the recent window before distilling.</summary>
    public int DistillAfterMessageCount { get; set; } = 12;
    public int MaxDistilledMemoryCharacters { get; set; } = 2000;
}
