using System.ComponentModel.DataAnnotations;

namespace Sentium.Backup.Options;

/// <summary>
/// Bound from the <c>Backup</c> configuration section. The database connection comes from the standard libpq
/// environment variables (<c>PGHOST</c>, <c>PGUSER</c>, <c>PGPASSWORD</c>, <c>PGDATABASE</c>), which
/// <c>pg_dump</c>, <c>pg_restore</c> and <c>psql</c> read themselves.
/// </summary>
internal sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>
    /// Directory the archives and <c>status.json</c> are written to, typically a NAS mount.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Destination { get; set; } = "/backups";

    /// <summary>
    /// Local time of day (container time zone, <c>TZ</c>) the nightly backup starts.
    /// </summary>
    public TimeOnly TimeOfDay { get; set; } = new(3, 0);

    [Range(1, 365)]
    public int KeepDaily { get; set; } = 7;

    [Range(0, 520)]
    public int KeepWeekly { get; set; } = 4;

    /// <summary>
    /// An age public key (<c>age1...</c>). When set, archives are encrypted to it; the matching identity
    /// must be kept off this host. Empty leaves archives unencrypted.
    /// </summary>
    public string? RecipientPublicKey { get; set; }

    /// <summary>
    /// The Sentium.Web data-protection key ring, mounted read-only for backups and read-write for a restore.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DataProtectionKeysPath { get; set; } = "/keys";

    /// <summary>
    /// Owner given to restored key files: the <c>APP_UID</c> Sentium.Web runs as in the .NET images.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string KeysOwner { get; set; } = "1654:1654";

    /// <summary>
    /// Scratch space for decrypting and unpacking an archive during a restore.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RestoreWorkDirectory { get; set; } = "/tmp/sentium-restore";

    public bool IsEncryptionEnabled => !string.IsNullOrWhiteSpace(RecipientPublicKey);
}
