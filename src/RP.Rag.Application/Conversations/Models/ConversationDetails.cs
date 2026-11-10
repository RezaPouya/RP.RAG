namespace RP.Rag.Application.Conversations.Models;

public sealed record ConversationDetails(
    Guid ConversationId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    string? DistilledMemory,
    IReadOnlyList<ConversationMessageDto> Messages,
    IReadOnlyList<SemanticMemoryItem>? SemanticFacts = null,
    IReadOnlyList<EpisodicMemoryItem>? Episodes = null);
