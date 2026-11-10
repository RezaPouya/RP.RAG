namespace RP.Rag.Application.Conversations.Models;

/// <summary>Per-request working memory. It is assembled for one Ask call and never persisted as a profile.</summary>
public sealed record WorkingMemory(
    string CurrentQuestion,
    IReadOnlyList<ConversationMessageDto> RecentMessages,
    IReadOnlyList<SemanticMemoryItem> SemanticFacts,
    IReadOnlyList<EpisodicMemoryItem> Episodes);
