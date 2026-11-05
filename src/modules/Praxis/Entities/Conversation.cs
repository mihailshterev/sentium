using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

public sealed class Conversation : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Model { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
