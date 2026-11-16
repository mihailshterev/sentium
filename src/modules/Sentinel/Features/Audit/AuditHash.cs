using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Sentium.Sentinel.Features.Audit;

/// <summary>
/// Lower-case hex SHA-256 of a text, so the audit trail can correlate prompts and payloads without storing them.
/// </summary>
internal static class AuditHash
{
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        var rented = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            Span<byte> hashBuffer = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(rented.AsSpan(0, written), hashBuffer);
            return Convert.ToHexStringLower(hashBuffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
