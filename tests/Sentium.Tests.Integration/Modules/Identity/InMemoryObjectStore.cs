using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sentium.Infrastructure.Storage;

namespace Sentium.Tests.Integration.Modules.Identity;

public sealed class InMemoryObjectStore : IObjectStore
{
    private readonly ConcurrentDictionary<(string Bucket, string Key), (byte[] Content, string ContentType)> _objects = new();

    public bool Contains(string bucket, string key) => _objects.ContainsKey((bucket, key));

    public async Task PutAsync(
        string bucket,
        string key,
        Stream content,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _objects[(bucket, key)] = (buffer.ToArray(), contentType ?? "application/octet-stream");
    }

    public Task<StoredObject?> OpenReadAsync(string bucket, string key, CancellationToken ct = default)
    {
        var stored = _objects.TryGetValue((bucket, key), out var entry)
            ? new StoredObject(new MemoryStream(entry.Content, writable: false), entry.ContentType, entry.Content.Length, new Dictionary<string, string>())
            : null;

        return Task.FromResult(stored);
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        _objects.TryRemove((bucket, key), out _);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        foreach (var id in _objects.Keys.Where(k => k.Bucket == bucket && k.Key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _objects.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ListBucketsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        foreach (var bucket in _objects.Keys.Select(k => k.Bucket).Distinct())
        {
            yield return bucket;
        }
    }

    public async IAsyncEnumerable<string> ListKeysAsync(string bucket, string? prefix = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        foreach (var (_, key) in _objects.Keys.Where(k => k.Bucket == bucket && k.Key.StartsWith(prefix ?? string.Empty, StringComparison.Ordinal)))
        {
            yield return key;
        }
    }

    public Task PingAsync(CancellationToken ct = default) => Task.CompletedTask;
}
