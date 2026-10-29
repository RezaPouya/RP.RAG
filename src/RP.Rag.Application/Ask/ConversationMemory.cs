namespace RP.RAG.Application.Ask;

public enum ConversationMessageRole
{
    User,
    Assistant
}

public sealed record ConversationMessageDto(
    int Sequence,
    string Role,
    string Content,
    DateTime CreatedAt);

public sealed record ConversationMemory(
    Guid ConversationId,
    int MessageCount,
    string? DistilledMemory,
    IReadOnlyList<ConversationMessageDto> RecentMessages,
    IReadOnlyList<SemanticMemoryItem>? SemanticFacts = null,
    IReadOnlyList<EpisodicMemoryItem>? Episodes = null);

/// <summary>A source-backed user-stated fact or preference; scope is one conversation.</summary>
public sealed record SemanticMemoryItem(string Key, string Value, string Evidence, int SourceSequence);

/// <summary>A source-backed event or outcome in the current conversation.</summary>
public sealed record EpisodicMemoryItem(
    string Summary,
    string Evidence,
    int StartSequence,
    int EndSequence,
    DateTime OccurredAt);

/// <summary>Per-request working memory. It is assembled for one Ask call and never persisted as a profile.</summary>
public sealed record WorkingMemory(
    string CurrentQuestion,
    IReadOnlyList<ConversationMessageDto> RecentMessages,
    IReadOnlyList<SemanticMemoryItem> SemanticFacts,
    IReadOnlyList<EpisodicMemoryItem> Episodes);

public sealed record ConversationDetails(
    Guid ConversationId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    string? DistilledMemory,
    IReadOnlyList<ConversationMessageDto> Messages,
    IReadOnlyList<SemanticMemoryItem>? SemanticFacts = null,
    IReadOnlyList<EpisodicMemoryItem>? Episodes = null);

public interface IConversationMemoryService
{
    Task<ConversationDetails> CreateAsync(CancellationToken cancellationToken = default);

    Task<ConversationDetails?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ConversationMemory?> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<int> AddMessageAsync(
        Guid conversationId,
        ConversationMessageRole role,
        string content,
        CancellationToken cancellationToken = default);

    Task DistillIfNeededAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
