using Amazon.S3;
using Amazon.S3.Model;

namespace Sentium.Infrastructure.Storage;

/// <summary>
/// A seekable read stream over one S3 object. Reads continue the open response body; a seek only moves the position,
/// and the next read reopens the object as a ranged GET from there. Every reopen is pinned to the first response's
/// ETag, so an object replaced mid-read fails the read instead of splicing two versions together.
/// </summary>
internal sealed class S3ObjectReadStream : Stream
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _key;
    private readonly string _etag;

    private GetObjectResponse? _response;
    private long _position;
    private long _bodyPosition;

    public S3ObjectReadStream(IAmazonS3 s3, string bucket, string key, GetObjectResponse response)
    {
        _s3 = s3;
        _bucket = bucket;
        _key = key;
        _etag = response.ETag;
        _response = response;
        Length = response.ContentLength;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty || _position >= Length)
        {
            return 0;
        }

        if (_response is null || _bodyPosition != _position)
        {
            await ReopenAsync(cancellationToken);
        }

        var read = await _response!.ResponseStream.ReadAsync(buffer, cancellationToken);
        _position += read;
        _bodyPosition += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
        _position = target;
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _response?.Dispose();
            _response = null;
        }

        base.Dispose(disposing);
    }

    private async Task ReopenAsync(CancellationToken ct)
    {
        _response?.Dispose();
        _response = null;

        _response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucket,
            Key = _key,
            ByteRange = new ByteRange(_position, Length - 1),
            EtagToMatch = _etag,
        }, ct);

        _bodyPosition = _position;
    }
}
