namespace Sentium.Vigil.Features.Backups;

internal interface IBackupClient
{
    /// <summary>
    /// The sidecar's status, or <see langword="null"/> when it cannot be reached or answers with an error.
    /// </summary>
    Task<BackupStatusDto?> GetStatusAsync(CancellationToken ct);

    Task<BackupStartResult> StartAsync(CancellationToken ct);
}
