namespace Sentium.Codex.Contracts.Models;

public static class KnowledgeCollections
{
    public const string KnowledgeBase = "knowledge_base";
    public const string AgentLearnings = "agent_learnings";
    public const string UserMemories = "user_memories";

    /// <summary>
    /// Notes synced from a user's Obsidian vault by the Memoria module; always user-scoped.
    /// </summary>
    public const string VaultNotes = "vault_notes";

    public static readonly string[] All = [KnowledgeBase, AgentLearnings, UserMemories, VaultNotes];
}
