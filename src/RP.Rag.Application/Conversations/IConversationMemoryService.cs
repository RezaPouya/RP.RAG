using RP.Rag.Application.Conversations.Models;

namespace RP.Rag.Application.Conversations;

public interface IConversationMemoryService
{
    Task<ConversationDetails> CreateAsync(CancellationToken cancellationToken = default);
    Task<ConversationDetails?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the 4 phases of memory: Recent messages, Semantic facts, Episodic events.
    /// (Procedural memory is handled by the PromptBuilder via embedded markdown files).
    /// </summary>
    Task<ConversationMemory?> GetMemoryAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a user or assistant message to the conversation.
    /// </summary>
    Task<int> AddMessageAsync(
        Guid conversationId,
        ConversationMessageRole role,
        string content,
        CancellationToken cancellationToken = default);


    Task DistillIfNeededAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
