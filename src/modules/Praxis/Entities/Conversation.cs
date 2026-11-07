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
    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
