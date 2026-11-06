namespace RP.Rag.Application.Conversations.Models;

public sealed record ConversationMessageDto(
    int Sequence,
    string Role,
    string Content,
    DateTime CreatedAt);
