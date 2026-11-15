using System.Text.Json;

namespace Sentium.Backup.Archives;

/// <summary>
/// The first entry of every archive. A restore trusts nothing in the archive that does not match it.
/// </summary>
internal sealed record BackupManifest(int FormatVersion, string SentiumVersion, DateTimeOffset CreatedAt, IReadOnlyList<BackupPart> Parts)
{
    public const string EntryName = "manifest.json";
    public const int CurrentFormatVersion = 1;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
