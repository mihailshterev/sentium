namespace Sentium.AgentRuntime.Core.Rag.Models;

public static class KnowledgeCollections
{
    public const string KnowledgeBase = "knowledge_base";
    public const string AgentLearnings = "agent_learnings";
    public const string UserMemories = "user_memories";
    public static readonly string[] All = [KnowledgeBase, AgentLearnings, UserMemories];
}
