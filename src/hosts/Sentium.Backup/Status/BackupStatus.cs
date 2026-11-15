using Sentium.Backup.Archives;

namespace Sentium.Backup.Status;

/// <summary>
/// What the sidecar serves on <c>GET /api/backup/status</c> and writes to <c>status.json</c> next to the archives.
/// <see cref="Backups"/> is newest first, and every entry is a verified backup.
/// </summary>
internal sealed record BackupStatus(
    string SentiumVersion,
    DateTimeOffset ServiceStartedAt,
    bool DestinationReady,
    string? DestinationProblem,
    bool Running,
    DateTimeOffset? NextRunAt,
    BackupRunResult? LastRun,
    IReadOnlyList<BackupArchive> Backups,
    int KeepDaily,
    int KeepWeekly,
    bool EncryptionEnabled);
