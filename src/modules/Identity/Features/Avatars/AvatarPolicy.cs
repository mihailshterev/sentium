namespace Sentium.Identity.Features.Avatars;

/// <summary>
/// The portal crops and re-encodes pictures before uploading, so only the formats a browser canvas produces are
/// accepted. The stored content type always comes from the file's leading bytes, never from the client.
/// </summary>
internal static class AvatarPolicy
{
    public const long MaxBytes = 1024 * 1024;

    /// <summary>
    /// How many leading bytes <see cref="DetectContentType"/> needs.
    /// </summary>
    public const int HeaderLength = 12;

    /// <summary>
    /// Content type for the given leading bytes, or <see langword="null"/> when the format is not allowed.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.Length >= HeaderLength && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
