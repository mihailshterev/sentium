namespace Sentium.Identity.Features.Avatars;

internal static class AvatarUrls
{
    /// <summary>
    /// The picture's URL, versioned by <paramref name="updatedAt"/> so a replaced picture gets a new URL, or
    /// <see langword="null"/> when the user has no picture.
    /// </summary>
    public static string? For(Guid userId, DateTimeOffset? updatedAt) =>
        updatedAt is { } version
            ? $"/{IdentityRoutes.Prefix}/users/{userId}/avatar?v={version.ToUnixTimeMilliseconds()}"
            : null;
}
