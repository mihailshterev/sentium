namespace Sentium.Identity.Entities;

/// <summary>
/// A short-lived, single-use code a user creates in the portal and types into the Satelles app. The row is deleted when
/// the code is used.
/// </summary>
internal sealed class DevicePairingCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Hex SHA-256 of the normalized code; the code itself is shown once and never stored.
    /// </summary>
    public string CodeHash { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
