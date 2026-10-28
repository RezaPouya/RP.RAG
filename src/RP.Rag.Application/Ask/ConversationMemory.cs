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
    IReadOnlyList<ConversationMessageDto> RecentMessages);

public sealed record ConversationDetails(
    Guid ConversationId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    string? DistilledMemory,
    IReadOnlyList<ConversationMessageDto> Messages);

public interface IConversationMemoryService
{
    Task<ConversationDetails> CreateAsync(CancellationToken cancellationToken = default);

    Task<ConversationDetails?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ConversationMemory?> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<int> AddMessageAsync(
        Guid conversationId,
        ConversationMessageRole role,
        string content,
        CancellationToken cancellationToken = default);

    Task DistillIfNeededAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
