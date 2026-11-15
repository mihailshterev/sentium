namespace Sentium.Identity.Entities;

/// <summary>
/// One Windows sign-in on a household PC, paired to exactly one user. It never changes owner: handing a PC to someone
/// else means unpairing and pairing again.
/// </summary>
internal sealed class Device
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public ApplicationUser Owner { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// Hex SHA-256 of the Device token. The token itself is returned once at pairing and never stored.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    public string? AppVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Written at most every few minutes, not on every request.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }
}
