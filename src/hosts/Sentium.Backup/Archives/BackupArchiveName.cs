using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Sentium.Backup.Archives;

/// <summary>
/// Archives are named <c>sentium-yyyyMMddTHHmmssZ.tar</c> (<c>.tar.age</c> when encrypted) by the UTC time the
/// run started. Only names in this form count as backups; in-progress files carry a leading dot and a
/// <c>.partial</c> suffix so they can never be mistaken for one.
/// </summary>
internal static class BackupArchiveName
{
    public const string PlainExtension = ".tar";
    public const string EncryptedExtension = ".tar.age";
    public const string PartialSuffix = ".partial";

    private const string Prefix = "sentium-";
    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public static string Format(DateTimeOffset createdAt, bool encrypted)
    {
        var stamp = createdAt.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        return Prefix + stamp + (encrypted ? EncryptedExtension : PlainExtension);
    }

    public static string Partial(string finalName) => "." + finalName + PartialSuffix;

    public static bool TryParse(string fileName, [NotNullWhen(true)] out BackupArchive? archive)
    {
        archive = null;
        if (!fileName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encrypted = fileName.EndsWith(EncryptedExtension, StringComparison.Ordinal);
        if (!encrypted && !fileName.EndsWith(PlainExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var extensionLength = encrypted ? EncryptedExtension.Length : PlainExtension.Length;
        var stamp = fileName[Prefix.Length..^extensionLength];
        if (!DateTime.TryParseExact(stamp, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var createdAt))
        {
            return false;
        }

        archive = new BackupArchive(fileName, new DateTimeOffset(createdAt, TimeSpan.Zero), 0, encrypted);
        return true;
    }
}
