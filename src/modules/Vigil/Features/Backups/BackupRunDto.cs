namespace Sentium.Vigil.Features.Backups;

internal sealed record BackupRunDto(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Trigger,
    bool Succeeded,
    string? Archive,
    string? Error);
