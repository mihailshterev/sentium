using System.Formats.Tar;
using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Parts;

/// <summary>
/// Copies every blob of every container through the blob API (see ADR 0004). Each blob is read whole, so it is
/// consistent on its own; a blob written during the run may be missing and is picked up by the next one.
/// </summary>
internal sealed class BlobPart(BlobServiceClient blobs, ILogger<BlobPart> logger)
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
            await foreach (var container in blobs.GetBlobContainersAsync(cancellationToken: ct))
            {
                var containerClient = blobs.GetBlobContainerClient(container.Name);
                await foreach (var item in containerClient.GetBlobsAsync(new GetBlobsOptions { Traits = BlobTraits.Metadata }, ct))
                {
                    var entryName = DataEntryPrefix + index.Count.ToString("D6", CultureInfo.InvariantCulture);
                    if (!await TryDownloadAsync(containerClient.GetBlobClient(item.Name), scratchFile, ct))
                    {
                        logger.LogInformation("Blob {Container}/{Blob} was deleted during the backup; skipping it", container.Name, item.Name);
                        continue;
                    }

                    await writer.WriteEntryAsync(scratchFile, entryName, ct);
                    index.Add(new BlobIndexEntry(
                        entryName,
                        container.Name,
                        item.Name,
                        new FileInfo(scratchFile).Length,
                        item.Properties.ContentType,
                        item.Metadata));
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
        var created = new HashSet<string>(StringComparer.Ordinal);
        var restored = 0;

        await using var input = File.OpenRead(tarPath);
        await using var reader = new TarReader(input);
        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (entry.DataStream is null || !index.TryGetValue(entry.Name, out var blob))
            {
                continue;
            }

            var container = blobs.GetBlobContainerClient(blob.Container);
            if (created.Add(blob.Container))
            {
                await container.CreateIfNotExistsAsync(cancellationToken: ct);
            }

            await container.GetBlobClient(blob.Name).UploadAsync(entry.DataStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = blob.ContentType },
                Metadata = blob.Metadata
            }, ct);
            restored++;
        }

        if (restored != index.Count)
        {
            throw new BackupFailedException($"The blob index lists {index.Count} blobs but the archive holds {restored}.");
        }

        return restored;
    }

    /// <summary>
    /// Waits for the blob service, which may still be starting when a restore begins.
    /// </summary>
    public async Task WaitUntilReachableAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await blobs.GetPropertiesAsync(ct);
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
        await foreach (var container in blobs.GetBlobContainersAsync(cancellationToken: ct))
        {
            await foreach (var _ in blobs.GetBlobContainerClient(container.Name).GetBlobsAsync(cancellationToken: ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task DeleteAllContainersAsync(CancellationToken ct)
    {
        await foreach (var container in blobs.GetBlobContainersAsync(cancellationToken: ct))
        {
            await blobs.DeleteBlobContainerAsync(container.Name, cancellationToken: ct);
        }
    }

    private static async Task<bool> TryDownloadAsync(BlobClient blob, string path, CancellationToken ct)
    {
        try
        {
            await blob.DownloadToAsync(path, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return false;
        }
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
