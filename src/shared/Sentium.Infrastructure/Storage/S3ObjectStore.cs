using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Sentium.Infrastructure.Storage;

/// <summary>
/// <see cref="IObjectStore"/> on any S3-compatible server (SeaweedFS in the shipped deployments). The client is
/// configured for self-hosted servers rather than AWS: path-style addressing (no bucket subdomains to resolve), plain
/// SigV4 over the whole payload instead of aws-chunked uploads, and checksums only where the API requires them.
/// Those are the corners where S3-compatible servers differ most from AWS.
/// </summary>
internal sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private const string DefaultContentType = "application/octet-stream";
    private const string MetadataHeaderPrefix = "x-amz-meta-";
    private const string NoSuchBucket = "NoSuchBucket";
    private const int CopyBufferSize = 81920;

    private static readonly HashSet<string> BucketExistsCodes = new(StringComparer.Ordinal)
    {
        "BucketAlreadyOwnedByYou",
        "BucketAlreadyExists",
    };

    private readonly AmazonS3Client _s3;
    private readonly ConcurrentDictionary<string, byte> _knownBuckets = new(StringComparer.Ordinal);

    public S3ObjectStore(ObjectStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint.ToString(),
            AuthenticationRegion = settings.Region,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        _s3 = new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
    }

    public async Task PutAsync(
        string bucket,
        string key,
        Stream content,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        await EnsureBucketAsync(bucket, ct);

        // A signed upload needs the length and a payload it can hash and rewind; anything else is buffered first.
        if (content is { CanSeek: true, Position: 0 })
        {
            await PutSeekableAsync(bucket, key, content, contentType, metadata, ct);
            return;
        }

        await using var buffer = await BufferAsync(content, ct);
        await PutSeekableAsync(bucket, key, buffer, contentType, metadata, ct);
    }

    public async Task<StoredObject?> OpenReadAsync(string bucket, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(key);

        GetObjectResponse response;
        try
        {
            response = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var contentType = string.IsNullOrEmpty(response.Headers.ContentType) ? DefaultContentType : response.Headers.ContentType;

        return new StoredObject(
            new S3ObjectReadStream(_s3, bucket, key, response),
            contentType,
            response.ContentLength,
            ReadMetadata(response.Metadata));
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            await _s3.DeleteObjectAsync(bucket, key, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }

    public async Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentNullException.ThrowIfNull(prefix);

        // One request per object: multi-object DeleteObjects needs a Content-MD5/checksum header that S3-compatible
        // servers accept inconsistently, and these prefixes hold a handful of objects.
        var keys = new List<string>();
        await foreach (var key in ListKeysAsync(bucket, prefix, ct))
        {
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            await DeleteAsync(bucket, key, ct);
        }
    }

    public async IAsyncEnumerable<string> ListBucketsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await _s3.ListBucketsAsync(new ListBucketsRequest(), ct);

        foreach (var bucket in response.Buckets ?? [])
        {
            yield return bucket.BucketName;
        }
    }

    public async IAsyncEnumerable<string> ListKeysAsync(
        string bucket,
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);

        var request = new ListObjectsV2Request { BucketName = bucket, Prefix = string.IsNullOrEmpty(prefix) ? null : prefix };

        while (await ListPageAsync(request, ct) is { } page)
        {
            foreach (var item in page.S3Objects ?? [])
            {
                yield return item.Key;
            }

            if (page.IsTruncated != true)
            {
                yield break;
            }

            request.ContinuationToken = page.NextContinuationToken;
        }
    }

    public async Task PingAsync(CancellationToken ct = default)
    {
        await _s3.ListBucketsAsync(new ListBucketsRequest(), ct);
    }

    public void Dispose() => _s3.Dispose();

    private async Task PutSeekableAsync(
        string bucket,
        string key,
        Stream content,
        string? contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = string.IsNullOrEmpty(contentType) ? DefaultContentType : contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false,
        };

        // Metadata travels as HTTP headers, which only carry ASCII; escaping keeps any file name intact.
        foreach (var (name, value) in metadata ?? new Dictionary<string, string>())
        {
            request.Metadata[name] = Uri.EscapeDataString(value);
        }

        await _s3.PutObjectAsync(request, ct);
    }

    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        if (_knownBuckets.ContainsKey(bucket))
        {
            return;
        }

        try
        {
            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is not null && BucketExistsCodes.Contains(ex.ErrorCode))
        {
            // Created earlier, by this or another process.
        }

        _knownBuckets.TryAdd(bucket, 0);
    }

    private async Task<ListObjectsV2Response?> ListPageAsync(ListObjectsV2Request request, CancellationToken ct)
    {
        try
        {
            return await _s3.ListObjectsV2Async(request, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == NoSuchBucket)
        {
            return null;
        }
    }

    private static async Task<FileStream> BufferAsync(Stream content, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var buffer = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, CopyBufferSize, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    private static Dictionary<string, string> ReadMetadata(MetadataCollection metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in metadata.Keys)
        {
            var name = header.StartsWith(MetadataHeaderPrefix, StringComparison.OrdinalIgnoreCase)
                ? header[MetadataHeaderPrefix.Length..]
                : header;

            result[name] = Uri.UnescapeDataString(metadata[header]);
        }

        return result;
    }
}
