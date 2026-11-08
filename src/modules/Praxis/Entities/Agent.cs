using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

public sealed class Agent : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Model { get; set; } = string.Empty;
    public List<string>? Tools { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
