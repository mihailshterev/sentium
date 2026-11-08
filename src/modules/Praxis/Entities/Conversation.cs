using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

public sealed class Conversation : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;

    /// <summary>
    /// Whether the title is still the placeholder and should be generated from the conversation's first prompt. Cleared
    /// once a title is generated or the user renames the conversation, so a manual title is never overwritten.
    /// </summary>
    public bool IsTitlePending { get; set; }

    public string Model { get; set; } = null!;

    /// <summary>
    /// The agent this conversation talks to (a built-in specialist or one of the owner's custom agents);
    /// <see langword="null"/> for the general assistant. Fixed at creation, so resuming or reopening the chat
    /// elsewhere keeps the same agent.
    /// </summary>
    public string? AgentName { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
