using Microsoft.AspNetCore.Identity;

namespace Sentium.Identity.Entities;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }

    /// <summary>
    /// When the profile picture was last replaced, or <see langword="null"/> when the user has none. Doubles as the
    /// version in the picture's URL, so browsers may cache it indefinitely.
    /// </summary>
    public DateTimeOffset? AvatarUpdatedAt { get; set; }
}
