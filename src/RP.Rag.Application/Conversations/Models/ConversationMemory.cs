namespace RP.Rag.Application.Conversations.Models;

public sealed record ConversationMemory(
    Guid ConversationId,
    int MessageCount,
    string? DistilledMemory,
    IReadOnlyList<ConversationMessageDto> RecentMessages,
    IReadOnlyList<SemanticMemoryItem>? SemanticFacts = null,
    IReadOnlyList<EpisodicMemoryItem>? Episodes = null);
