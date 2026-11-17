using System.Formats.Tar;
using System.Globalization;
using System.Text.Json;
using Sentium.Backup.Tools;
using Sentium.Infrastructure.Storage;

namespace Sentium.Backup.Parts;

/// <summary>
/// Copies every object of every bucket through the object storage API (see ADR 0004). Each object is read whole, so
/// it is consistent on its own; an object written during the run may be missing and is picked up by the next one.
/// The archive keeps the blob-era names: an index entry's <see cref="BlobIndexEntry.Container"/> is the bucket.
/// </summary>
internal sealed class BlobPart(IObjectStore store, ILogger<BlobPart> logger)
{
    private const string IndexEntryName = "index.json";
    private const string DataEntryPrefix = "data/";
    private const int ReadinessAttempts = 30;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReadinessDelay = TimeSpan.FromSeconds(2);

    public async Task<int> ExportAsync(string tarPath, string scratchDirectory, CancellationToken ct)
    {
        var index = new List<BlobIndexEntry>();
        var scratchFile = Path.Combine(scratchDirectory, "blob.tmp");

        await using (var output = File.Create(tarPath))
        await using (var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: false))
        {
            await foreach (var bucket in store.ListBucketsAsync(ct))
            {
                await foreach (var key in store.ListKeysAsync(bucket, ct: ct))
                {
                    var entryName = DataEntryPrefix + index.Count.ToString("D6", CultureInfo.InvariantCulture);
                    var stored = await TryDownloadAsync(bucket, key, scratchFile, ct);
                    if (stored is null)
                    {
                        logger.LogInformation("Blob {Container}/{Blob} was deleted during the backup; skipping it", bucket, key);
                        continue;
                    }

                    await writer.WriteEntryAsync(scratchFile, entryName, ct);
                    index.Add(new BlobIndexEntry(
                        entryName,
                        bucket,
                        key,
                        new FileInfo(scratchFile).Length,
                        stored.ContentType,
                        new Dictionary<string, string>(stored.Metadata)));
                }
            }

            File.Delete(scratchFile);
            await WriteIndexAsync(writer, index, ct);
        }

        return index.Count;
    }

    public async Task<int> ImportAsync(string tarPath, CancellationToken ct)
    {
        var index = (await ReadIndexAsync(tarPath, ct)).ToDictionary(e => e.Entry, StringComparer.Ordinal);
        var restored = 0;

        await using var input = File.OpenRead(tarPath);
        await using var reader = new TarReader(input);
        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (entry.DataStream is null || !index.TryGetValue(entry.Name, out var blob))
            {
                continue;
            }

            await store.PutAsync(
                blob.Container,
                blob.Name,
                entry.DataStream,
                blob.ContentType,
                blob.Metadata?.AsReadOnly(),
                ct);
            restored++;
        }

        if (restored != index.Count)
        {
            throw new BackupFailedException($"The blob index lists {index.Count} blobs but the archive holds {restored}.");
        }

        return restored;
    }

    /// <summary>
    /// Waits for the object store, which may still be starting when a restore begins.
    /// </summary>
    public async Task WaitUntilReachableAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await store.PingAsync(ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < ReadinessAttempts)
            {
                await Task.Delay(ReadinessDelay, ct);
            }
        }
    }

    public async Task<bool> HasAnyBlobAsync(CancellationToken ct)
    {
        await foreach (var bucket in store.ListBucketsAsync(ct))
        {
            await foreach (var _ in store.ListKeysAsync(bucket, ct: ct))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Empties every bucket. The buckets themselves stay; writes recreate any that are missing anyway.
    /// </summary>
    public async Task DeleteAllBlobsAsync(CancellationToken ct)
    {
        var buckets = new List<string>();
        await foreach (var bucket in store.ListBucketsAsync(ct))
        {
            buckets.Add(bucket);
        }

        foreach (var bucket in buckets)
        {
            await store.DeletePrefixAsync(bucket, string.Empty, ct);
        }
    }

    private async Task<StoredObject?> TryDownloadAsync(string bucket, string key, string path, CancellationToken ct)
    {
        var stored = await store.OpenReadAsync(bucket, key, ct);
        if (stored is null)
        {
            return null;
        }

        await using (stored.Content)
        await using (var file = File.Create(path))
        {
            await stored.Content.CopyToAsync(file, ct);
        }

        return stored;
    }

    private static async Task WriteIndexAsync(TarWriter writer, List<BlobIndexEntry> index, CancellationToken ct)
    {
        using var json = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(index, JsonOptions));
        var entry = new PaxTarEntry(TarEntryType.RegularFile, IndexEntryName) { DataStream = json };
        await writer.WriteEntryAsync(entry, ct);
    }

    /// <summary>
    /// The index is the last entry (it is only complete once every blob was read), so a restore reads the tar twice.
    /// </summary>
    private static async Task<List<BlobIndexEntry>> ReadIndexAsync(string tarPath, CancellationToken ct)
    {
        await using var input = File.OpenRead(tarPath);
        await using var reader = new TarReader(input);
        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (entry.Name == IndexEntryName && entry.DataStream is not null)
            {
                return await JsonSerializer.DeserializeAsync<List<BlobIndexEntry>>(entry.DataStream, JsonOptions, ct)
                    ?? throw new BackupFailedException("The blob index is empty.");
            }
        }

        throw new BackupFailedException("The blob part has no index.");
    }
}
