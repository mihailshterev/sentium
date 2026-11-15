namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// The sidecar's <c>GET /api/backup/status</c> response. <see cref="Backups"/> is newest first and holds only
/// verified archives.
/// </summary>
internal sealed record BackupStatusDto(
    string SentiumVersion,
    DateTimeOffset ServiceStartedAt,
    bool DestinationReady,
    string? DestinationProblem,
    bool Running,
    DateTimeOffset? NextRunAt,
    BackupRunDto? LastRun,
    IReadOnlyList<BackupArchiveDto> Backups,
    int KeepDaily,
    int KeepWeekly,
    bool EncryptionEnabled);
