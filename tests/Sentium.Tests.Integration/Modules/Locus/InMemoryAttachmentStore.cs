using System.Collections.Concurrent;
using Sentium.Locus.Features.Attachments;

namespace Sentium.Tests.Integration.Modules.Locus;

public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Blobs[blobName] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string blobName, CancellationToken ct = default)
        => Task.FromResult<Stream?>(Blobs.TryGetValue(blobName, out var data) ? new MemoryStream(data, writable: false) : null);

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        Blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in Blobs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Blobs.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
