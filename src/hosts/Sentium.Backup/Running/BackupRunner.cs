using System.Formats.Tar;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentium.Backup.Archives;
using Sentium.Backup.Options;
using Sentium.Backup.Parts;
using Sentium.Backup.Status;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Running;

/// <summary>
/// One backup: stage the parts in a work directory on the destination, pack them behind a manifest into a
/// partial archive, verify it, optionally encrypt it, and only then give it its final name. Old archives are
/// pruned afterwards. Callers serialize runs; see <see cref="BackupCoordinator"/>.
/// </summary>
internal sealed class BackupRunner(
    BackupCatalog catalog,
    BlobPart blobPart,
    KeysPart keysPart,
    IOptions<BackupOptions> options,
    TimeProvider time,
    ILogger<BackupRunner> logger)
{
    public async Task<BackupArchive> RunAsync(CancellationToken ct)
    {
        catalog.EnsureReady();
        catalog.RemoveLeftovers();

        var startedAt = time.GetUtcNow();
        var encrypted = options.Value.IsEncryptionEnabled;
        var finalName = BackupArchiveName.Format(startedAt, encrypted);
        var workDirectory = Path.Combine(catalog.Destination, BackupCatalog.WorkDirectoryPrefix + startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        var plainPartial = Path.Combine(catalog.Destination, BackupArchiveName.Partial(BackupArchiveName.Format(startedAt, encrypted: false)));
        var finalPartial = Path.Combine(catalog.Destination, BackupArchiveName.Partial(finalName));

        Directory.CreateDirectory(workDirectory);
        try
        {
            var manifest = await StagePartsAsync(workDirectory, startedAt, ct);
            await PackAsync(manifest, workDirectory, plainPartial, ct);
            DeleteStagedParts(workDirectory);

            await ArchiveReader.VerifyAsync(plainPartial, Path.Combine(workDirectory, "verify"), extractAll: false, ct);

            if (encrypted)
            {
                await AgeTool.EncryptAsync(plainPartial, finalPartial, options.Value.RecipientPublicKey!, ct);
                File.Delete(plainPartial);
            }

            var finalPath = Path.Combine(catalog.Destination, finalName);
            File.Move(finalPartial, finalPath);
            logger.LogInformation("Backup {Archive} written and verified ({Bytes} bytes)", finalName, new FileInfo(finalPath).Length);

            Prune();
            return new BackupArchive(finalName, startedAt, new FileInfo(finalPath).Length, encrypted);
        }
        finally
        {
            TryDelete(() => Directory.Delete(workDirectory, recursive: true));
            TryDelete(() => File.Delete(plainPartial));
            TryDelete(() => File.Delete(finalPartial));
        }
    }

    private async Task<BackupManifest> StagePartsAsync(string workDirectory, DateTimeOffset startedAt, CancellationToken ct)
    {
        await PostgresTools.DumpAsync(Path.Combine(workDirectory, BackupPartNames.Database), ct);

        var blobCount = await blobPart.ExportAsync(Path.Combine(workDirectory, BackupPartNames.Blobs), workDirectory, ct);
        var keyCount = await keysPart.ExportAsync(Path.Combine(workDirectory, BackupPartNames.Keys), ct);
        logger.LogInformation("Staged the database, {Blobs} blobs and {Keys} data-protection keys", blobCount, keyCount);

        var parts = new List<BackupPart>();
        foreach (var name in BackupPartNames.All)
        {
            var (size, sha256) = await ArchiveReader.HashFileAsync(Path.Combine(workDirectory, name), ct);
            parts.Add(new BackupPart(name, size, sha256));
        }

        return new BackupManifest(BackupManifest.CurrentFormatVersion, SentiumVersion.CurrentText, startedAt, parts);
    }

    private static async Task PackAsync(BackupManifest manifest, string workDirectory, string archivePath, CancellationToken ct)
    {
        await using var output = File.Create(archivePath);
        await using var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: false);

        using (var json = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(manifest, BackupManifest.JsonOptions)))
        {
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, BackupManifest.EntryName) { DataStream = json }, ct);
        }

        foreach (var part in manifest.Parts)
        {
            await writer.WriteEntryAsync(Path.Combine(workDirectory, part.Name), part.Name, ct);
        }
    }

    private static void DeleteStagedParts(string workDirectory)
    {
        foreach (var name in BackupPartNames.All)
        {
            File.Delete(Path.Combine(workDirectory, name));
        }
    }

    private void Prune()
    {
        var doomed = RetentionPolicy.SelectForDeletion(catalog.List(), options.Value.KeepDaily, options.Value.KeepWeekly);
        foreach (var archive in doomed)
        {
            catalog.Delete(archive);
            logger.LogInformation("Pruned backup {Archive}", archive.Name);
        }
    }

    private void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not clean up after the backup run");
        }
    }
}
