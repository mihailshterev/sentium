using Microsoft.Extensions.Options;
using Sentium.Backup.Archives;
using Sentium.Backup.Options;
using Sentium.Backup.Parts;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Restore;

/// <summary>
/// <c>restore</c> and <c>verify</c>, run by <c>infra/backup/restore.sh</c> with the application containers stopped.
/// Everything is checked before anything is written: the archive is decrypted, every part is verified against
/// the manifest, the version is compared, and the targets must be empty (or <c>--replace</c> given).
/// </summary>
internal sealed class RestoreCommand(BlobPart blobPart, KeysPart keysPart, IOptions<BackupOptions> options)
{
    public async Task<int> RunAsync(RestoreArguments arguments, CancellationToken ct)
    {
        if (!File.Exists(arguments.ArchivePath))
        {
            throw new BackupFailedException($"Archive {arguments.ArchivePath} not found.");
        }

        var workDirectory = Path.Combine(options.Value.RestoreWorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        try
        {
            var tarPath = await DecryptIfNeededAsync(arguments, workDirectory, ct);
            var partsDirectory = Path.Combine(workDirectory, "parts");

            Console.WriteLine($"Verifying {Path.GetFileName(arguments.ArchivePath)}...");
            var manifest = await ArchiveReader.VerifyAsync(tarPath, partsDirectory, extractAll: !arguments.VerifyOnly, ct);
            EnsureVersionIsNotNewer(manifest);
            Console.WriteLine($"OK: made {manifest.CreatedAt:u} by Sentium {manifest.SentiumVersion}; every part matches the manifest.");

            if (arguments.VerifyOnly)
            {
                return 0;
            }

            await blobPart.WaitUntilReachableAsync(ct);
            await PrepareTargetsAsync(arguments.Replace, ct);
            await RestorePartsAsync(partsDirectory, ct);

            Console.WriteLine();
            Console.WriteLine("Restore complete. Start the stack with: docker compose up -d");
            Console.WriteLine("Sentium.Web applies any newer database migrations when it starts.");
            return 0;
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    private static async Task<string> DecryptIfNeededAsync(RestoreArguments arguments, string workDirectory, CancellationToken ct)
    {
        if (!arguments.ArchivePath.EndsWith(BackupArchiveName.EncryptedExtension, StringComparison.Ordinal))
        {
            return arguments.ArchivePath;
        }

        if (arguments.IdentityPath is null)
        {
            throw new BackupFailedException("The archive is encrypted; pass the age identity file with --identity.");
        }

        Console.WriteLine("Decrypting...");
        var decrypted = Path.Combine(workDirectory, "archive.tar");
        await AgeTool.DecryptAsync(arguments.ArchivePath, decrypted, arguments.IdentityPath, ct);
        return decrypted;
    }

    private static void EnsureVersionIsNotNewer(BackupManifest manifest)
    {
        if (!SentiumVersion.TryParse(manifest.SentiumVersion, out var backupVersion))
        {
            throw new BackupFailedException($"The manifest names an unreadable Sentium version '{manifest.SentiumVersion}'.");
        }

        if (backupVersion > SentiumVersion.Current)
        {
            throw new BackupFailedException(
                $"The backup was made by Sentium {manifest.SentiumVersion}, newer than this installation ({SentiumVersion.CurrentText}). " +
                "Update this installation first.");
        }
    }

    private async Task PrepareTargetsAsync(bool replace, CancellationToken ct)
    {
        var occupied = new List<string>();
        if (await PostgresTools.CountUserTablesAsync(ct) > 0)
        {
            occupied.Add($"database {PostgresTools.DatabaseName}");
        }

        if (await blobPart.HasAnyBlobAsync(ct))
        {
            occupied.Add("blob storage");
        }

        if (keysPart.HasAnyKey())
        {
            occupied.Add("data-protection keys");
        }

        if (occupied.Count == 0)
        {
            return;
        }

        if (!replace)
        {
            throw new BackupFailedException(
                $"Not empty: {string.Join(", ", occupied)}. Restore into a wiped installation, or pass --replace to discard the current data.");
        }

        Console.WriteLine($"Discarding the current {string.Join(", ", occupied)}...");
        await PostgresTools.RecreateDatabaseAsync(ct);
        await blobPart.DeleteAllContainersAsync(ct);
        keysPart.DeleteAll();
    }

    private async Task RestorePartsAsync(string partsDirectory, CancellationToken ct)
    {
        Console.WriteLine("Restoring the database...");
        await PostgresTools.RestoreAsync(Path.Combine(partsDirectory, BackupPartNames.Database), ct);

        try
        {
            Console.WriteLine("Restoring blobs...");
            var blobs = await blobPart.ImportAsync(Path.Combine(partsDirectory, BackupPartNames.Blobs), ct);
            Console.WriteLine($"  {blobs} blobs");

            Console.WriteLine("Restoring data-protection keys...");
            var keys = await keysPart.ImportAsync(Path.Combine(partsDirectory, BackupPartNames.Keys), ct);
            Console.WriteLine($"  {keys} keys");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BackupFailedException(
                $"The database was restored but the files were not ({ex.Message}). Run the restore again with --replace.", ex);
        }
    }
}
