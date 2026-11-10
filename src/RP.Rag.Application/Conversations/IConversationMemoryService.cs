using RP.Rag.Application.Conversations.Models;

namespace RP.Rag.Application.Conversations;

public interface IConversationMemoryService
{
    Task<ConversationDetails> CreateAsync(string userId, CancellationToken cancellationToken = default);
    Task<ConversationDetails?> GetAsync(string userId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<List<ConversationDetails>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<ConversationMemory?> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<int> AddMessageAsync(
        Guid conversationId,
        ConversationMessageRole role,
        string content,
        IReadOnlyList<int>? productIds = null,
        CancellationToken cancellationToken = default);
    Task DistillIfNeededAsync(Guid conversationId, CancellationToken cancellationToken = default);
}