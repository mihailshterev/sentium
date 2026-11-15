using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentium.Backup.Archives;
using Sentium.Backup.Options;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Status;

/// <summary>
/// The destination directory: which archives it holds, whether it is the real (mounted) destination, and the
/// <c>status.json</c> kept beside the archives.
/// </summary>
internal sealed class BackupCatalog(IOptions<BackupOptions> options, ILogger<BackupCatalog> logger)
{
    /// <summary>
    /// The operator creates this file once in the destination. An unmounted NAS share leaves an empty directory
    /// behind; without the marker, backups would silently land on the local disk instead.
    /// </summary>
    public const string MarkerFileName = ".sentium-backups";

    public const string WorkDirectoryPrefix = ".work-";

    private const string StatusFileName = "status.json";

    public string Destination => options.Value.Destination;

    public string? FindDestinationProblem()
    {
        if (!Directory.Exists(Destination))
        {
            return $"The backup destination {Destination} does not exist.";
        }

        return File.Exists(Path.Combine(Destination, MarkerFileName))
            ? null
            : $"The backup destination is not mounted: {MarkerFileName} is missing from {Destination}.";
    }

    public void EnsureReady()
    {
        if (FindDestinationProblem() is { } problem)
        {
            throw new BackupFailedException(problem);
        }
    }

    public IReadOnlyList<BackupArchive> List()
    {
        if (!Directory.Exists(Destination))
        {
            return [];
        }

        var archives = new List<BackupArchive>();
        foreach (var file in new DirectoryInfo(Destination).EnumerateFiles())
        {
            if (BackupArchiveName.TryParse(file.Name, out var archive))
            {
                archives.Add(archive with { SizeBytes = file.Length });
            }
        }

        return [.. archives.OrderByDescending(a => a.CreatedAt)];
    }

    public void Delete(BackupArchive archive) => File.Delete(Path.Combine(Destination, archive.Name));

    /// <summary>
    /// Removes what a crashed or killed run left behind: its work directory and partial archives.
    /// </summary>
    public void RemoveLeftovers()
    {
        var destination = new DirectoryInfo(Destination);
        foreach (var directory in destination.EnumerateDirectories(WorkDirectoryPrefix + "*"))
        {
            directory.Delete(recursive: true);
        }

        foreach (var file in destination.EnumerateFiles("*" + BackupArchiveName.PartialSuffix))
        {
            file.Delete();
        }
    }

    public async Task WriteStatusAsync(BackupStatus status, CancellationToken ct)
    {
        if (FindDestinationProblem() is not null)
        {
            return;
        }

        var path = Path.Combine(Destination, StatusFileName);
        var temporary = path + BackupArchiveName.PartialSuffix;
        try
        {
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, status, BackupManifest.JsonOptions, ct);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not write {StatusFile}", path);
        }
    }

    /// <summary>
    /// The last run recorded before this process started, so a restart does not forget a failure.
    /// </summary>
    public async Task<BackupRunResult?> ReadLastRunAsync(CancellationToken ct)
    {
        var path = Path.Combine(Destination, StatusFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var status = await JsonSerializer.DeserializeAsync<BackupStatus>(stream, BackupManifest.JsonOptions, ct);
            return status?.LastRun;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Ignoring unreadable {StatusFile}", path);
            return null;
        }
    }
}
