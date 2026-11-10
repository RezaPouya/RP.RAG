namespace RP.Rag.Application.Conversations.Models;

/// <summary>A source-backed event or outcome in the current conversation.</summary>
public sealed record EpisodicMemoryItem(
    string Summary,
    string Evidence,
    int StartSequence,
    int EndSequence,
    DateTime OccurredAt);
