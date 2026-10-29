namespace RP.Rag.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
    public string? DistilledMemory { get; set; }
    public int DistilledThroughSequence { get; set; }
    /// <summary>JSON-serialized, conversation-scoped user-stated facts and preferences.</summary>
    public string? SemanticMemoryJson { get; set; }
    /// <summary>JSON-serialized, conversation-scoped events and outcomes.</summary>
    public string? EpisodicMemoryJson { get; set; }

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
