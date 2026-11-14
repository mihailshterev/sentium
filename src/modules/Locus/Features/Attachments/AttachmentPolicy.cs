using System.Text;
using System.Text.Unicode;
using Sentium.Locus.Common;

namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// Decides what may be uploaded. The stored content type always comes from the file's leading bytes, never
/// from the client, and only formats that are inert when served (images, PDF, plain text) are accepted -
/// SVG and HTML in particular are rejected because they can carry script.
/// </summary>
internal static class AttachmentPolicy
{
    /// <summary>
    /// How many leading bytes <see cref="DetectContentType"/> needs.
    /// </summary>
    public const int HeaderLength = 512;

    private static readonly string[] TextExtensions = [".txt", ".md", ".csv", ".log"];

    private static readonly string[] HeifBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1"];

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>
    /// Content type for the given leading bytes, or <see langword="null"/> when the format is not allowed.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header, string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(header[8..12]);
            if (HeifBrands.Contains(brand, StringComparer.Ordinal))
            {
                return brand is "mif1" or "msf1" ? "image/heif" : "image/heic";
            }
        }

        if (header.StartsWith("%PDF-"u8))
        {
            return "application/pdf";
        }

        var extension = Path.GetExtension(fileName);
        if (TextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) && LooksLikeText(header))
        {
            return "text/plain";
        }

        return null;
    }

    /// <summary>
    /// Strips any directory part and control characters, and caps the length (keeping the extension).
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = new string([.. name.Where(c => !char.IsControl(c))]).Trim();

        if (name.Length == 0)
        {
            return "attachment";
        }

        if (name.Length <= LocusLimits.FileNameMax)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length > 16)
        {
            extension = string.Empty;
        }

        return name[..(LocusLimits.FileNameMax - extension.Length)] + extension;
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> header)
    {
        if (header.Contains((byte)0))
        {
            return false;
        }

        // A multi-byte character may be cut at the end of the header; validate the rest strictly.
        var probe = header.Length > 4 ? header[..^4] : header;
        return Utf8.IsValid(probe) || probe.IsEmpty;
    }
}
