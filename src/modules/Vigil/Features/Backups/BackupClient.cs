using System.Net;
using System.Text.Json;

namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// Calls the backup sidecar with the internal API key.
/// </summary>
internal sealed class BackupClient(HttpClient http, ILogger<BackupClient> logger) : IBackupClient
{
    private const string StatusPath = "api/backup/status";
    private const string RunsPath = "api/backup/runs";

    public async Task<BackupStatusDto?> GetStatusAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(StatusPath, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The backup sidecar answered {Status} to a status request", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<BackupStatusDto>(ct);
        }
        catch (Exception ex) when (IsUnreachable(ex, ct))
        {
            logger.LogDebug(ex, "The backup sidecar is unreachable");
            return null;
        }
    }

    public async Task<BackupStartResult> StartAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync(RunsPath, content: null, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return BackupStartResult.AlreadyRunning;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The backup sidecar answered {Status} to a start request", (int)response.StatusCode);
                return BackupStartResult.Unavailable;
            }

            return BackupStartResult.Started;
        }
        catch (Exception ex) when (IsUnreachable(ex, ct))
        {
            logger.LogWarning(ex, "The backup sidecar is unreachable");
            return BackupStartResult.Unavailable;
        }
    }

    /// <summary>
    /// Transport failures and the client's own timeout, but not the caller giving up.
    /// </summary>
    private static bool IsUnreachable(Exception ex, CancellationToken ct)
        => ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested);
}
