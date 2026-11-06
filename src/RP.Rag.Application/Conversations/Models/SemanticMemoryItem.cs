namespace RP.Rag.Application.Conversations.Models;

/// <summary>A source-backed user-stated fact or preference; scope is one conversation.</summary>
public sealed record SemanticMemoryItem(string Key, string Value, string Evidence, int SourceSequence);
