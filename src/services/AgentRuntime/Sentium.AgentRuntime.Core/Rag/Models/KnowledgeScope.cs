namespace Sentium.AgentRuntime.Core.Rag.Models;

/// <summary>
/// Visibility scope for a knowledge-base entry. The knowledge base is shared across all users,
/// but users may also contribute private entries that only they (and Sovereigns) can retrieve.
/// </summary>
public static class KnowledgeScope
{
    public const string Shared = "shared";
    public const string User = "user";
}

/// <summary>
/// Restricts a knowledge-base search to the entries a particular user is allowed to see:
/// shared entries plus that user's own entries. When passed to a search, a <c>null</c>
/// <see cref="UserId"/> limits results to shared entries only (e.g. background/system agents).
/// </summary>
/// <remarks>
/// All three Qdrant collections (<c>knowledge_base</c>, <c>agent_learnings</c>, <c>user_memories</c>)
/// are scoped: callers build this filter from the current user. Global entries (those stored with
/// <c>scope=shared</c> and no <c>user_id</c>) are visible to everyone; per-user entries are restricted
/// to their owner and Sovereigns. A global learning keeps its origin only in <c>origin_user_id</c>
/// metadata, not in the scope <c>user_id</c>, so it still reads as shared.
/// Passing <c>null</c> for the whole filter disables scope filtering entirely; that branch is reserved
/// and is not currently used by any collection.
/// </remarks>
public sealed record KnowledgeScopeFilter(Guid? UserId);
