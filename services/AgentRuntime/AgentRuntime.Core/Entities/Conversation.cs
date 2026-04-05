namespace AgentRuntime.Core.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Model { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
