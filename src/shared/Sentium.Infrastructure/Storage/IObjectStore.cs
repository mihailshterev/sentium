namespace Sentium.Infrastructure.Storage;

/// <summary>
/// Object storage: named buckets of objects addressed by key. Modules depend on this, never on a storage SDK.
/// </summary>
public interface IObjectStore
{
    /// <summary>
    /// Writes <paramref name="content"/> from its current position, replacing any object with the same key. The
    /// bucket is created on first use. Metadata values may be any text; they come back unchanged from
    /// <see cref="OpenReadAsync"/>.
    /// </summary>
    Task PutAsync(
        string bucket,
        string key,
        Stream content,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Opens an object for reading, or returns <see langword="null"/> when it or its bucket does not exist. The
    /// stream is seekable (seeks become ranged reads), so it can serve HTTP range requests. The caller disposes it.
    /// </summary>
    Task<StoredObject?> OpenReadAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object. Idempotent: a missing object or bucket is not an error.
    /// </summary>
    Task DeleteAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes every object whose key starts with <paramref name="prefix"/>; an empty prefix empties the bucket.
    /// A missing bucket is not an error.
    /// </summary>
    Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default);

    IAsyncEnumerable<string> ListBucketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists the keys in a bucket, optionally only those starting with <paramref name="prefix"/>. A missing bucket
    /// lists nothing.
    /// </summary>
    IAsyncEnumerable<string> ListKeysAsync(string bucket, string? prefix = null, CancellationToken ct = default);

    /// <summary>
    /// Throws when the store cannot be reached or rejects the credentials.
    /// </summary>
    Task PingAsync(CancellationToken ct = default);
}
