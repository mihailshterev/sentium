namespace Sentium.Infrastructure.Storage;

/// <summary>
/// Stable references to stored objects, for rows that must outlive a storage endpoint: <c>s3://{bucket}/{key}</c>
/// names the object without the host it happens to live on.
/// </summary>
public static class ObjectUri
{
    public const string Scheme = "s3";

    public static Uri Create(string bucket, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var escapedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"{Scheme}://{bucket}/{escapedKey}");
    }

    /// <summary>
    /// The key of the object <paramref name="uri"/> refers to in <paramref name="bucket"/>, or <see langword="null"/>
    /// when it is not an <c>s3://</c> reference into that bucket.
    /// </summary>
    public static string? GetKey(Uri uri, string bucket)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrEmpty(bucket);

        if (!uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, bucket, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        return key.Length == 0 ? null : key;
    }
}
