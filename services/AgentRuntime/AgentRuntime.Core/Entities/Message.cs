namespace AgentRuntime.Core.Entities;

public sealed class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string? Thought { get; set; }
    public string? ToolCalls { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
}
