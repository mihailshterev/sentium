namespace Sentium.Backup.Status;

internal sealed record BackupRunResult(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Trigger,
    bool Succeeded,
    string? Archive,
    string? Error);
