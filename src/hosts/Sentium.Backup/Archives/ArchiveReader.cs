using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text.Json;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Archives;

/// <summary>
/// Opens an (unencrypted) archive and proves it whole: the manifest comes first, every part it lists is present
/// once with the recorded size and SHA-256, nothing else is in the archive, and the database dump lists cleanly.
/// </summary>
internal static class ArchiveReader
{
    /// <param name="extractAll">
    /// Extract every part (a restore). Otherwise only the database dump is extracted, because
    /// <c>pg_restore --list</c> needs a file, and the other parts are only hashed.
    /// </param>
    public static async Task<BackupManifest> VerifyAsync(string archivePath, string targetDirectory, bool extractAll, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);

        await using var input = File.OpenRead(archivePath);
        await using var reader = new TarReader(input);

        var manifest = await ReadManifestAsync(reader, ct);
        var expected = manifest.Parts.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (!expected.TryGetValue(entry.Name, out var part) || !seen.Add(entry.Name))
            {
                throw new BackupFailedException($"Unexpected entry '{entry.Name}' in the archive.");
            }

            // An empty part (no data-protection keys yet) is a zero-length entry, which has no data stream.
            var data = entry.DataStream ?? Stream.Null;
            var extract = extractAll || part.Name == BackupPartNames.Database;
            var (size, sha256) = await HashAsync(data, extract ? Path.Combine(targetDirectory, part.Name) : null, ct);
            if (size != part.SizeBytes || sha256 != part.Sha256)
            {
                throw new BackupFailedException($"Part '{part.Name}' does not match its checksum in the manifest.");
            }
        }

        var missing = expected.Keys.Except(seen).ToList();
        if (missing.Count > 0)
        {
            throw new BackupFailedException($"The archive is missing {string.Join(", ", missing)}.");
        }

        await PostgresTools.ListEntriesAsync(Path.Combine(targetDirectory, BackupPartNames.Database), ct);
        return manifest;
    }

    public static async Task<(long Size, string Sha256)> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await HashAsync(stream, copyTo: null, ct);
    }

    private static async Task<BackupManifest> ReadManifestAsync(TarReader reader, CancellationToken ct)
    {
        var entry = await reader.GetNextEntryAsync(copyData: false, ct);
        if (entry?.Name != BackupManifest.EntryName || entry.DataStream is null)
        {
            throw new BackupFailedException("Not a Sentium backup: the archive does not start with a manifest.");
        }

        BackupManifest? manifest;
        try
        {
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(entry.DataStream, BackupManifest.JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new BackupFailedException("The manifest is not valid JSON.", ex);
        }

        if (manifest is null || manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
        {
            throw new BackupFailedException($"Unsupported manifest format {manifest?.FormatVersion}.");
        }

        if (!BackupPartNames.All.All(name => manifest.Parts.Any(p => p.Name == name)))
        {
            throw new BackupFailedException("The manifest does not list every part.");
        }

        return manifest;
    }

    private static async Task<(long Size, string Sha256)> HashAsync(Stream source, string? copyTo, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var target = copyTo is null ? null : File.Create(copyTo);

        var buffer = new byte[81_920];
        long size = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            size += read;
            if (target is not null)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        return (size, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
}
