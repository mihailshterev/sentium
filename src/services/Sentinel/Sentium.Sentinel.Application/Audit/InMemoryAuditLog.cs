using Sentium.Sentinel.Core.Audit;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Sentium.Sentinel.Application.Audit;

/// <summary>
/// Thread-safe, bounded in-memory audit log (circular buffer).
/// For production deployments replace with a durable implementation (SQL, Seq, etc.).
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private const int MaxCapacity = 1_000;

    private readonly LinkedList<AuditRecord> _buffer = new();
    private readonly Lock _lock = new();

    public ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        try
        {
            lock (_lock)
            {
                _buffer.AddFirst(record);
                if (_buffer.Count > MaxCapacity)
                {
                    _buffer.RemoveLast();
                }
            }
        }
        catch
        {
            // Audit failures must never break the caller's flow.
        }

        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<AuditRecord> GetRecent(int count = 100)
    {
        lock (_lock)
        {
            return _buffer.Take(Math.Max(1, Math.Min(count, MaxCapacity))).ToList();
        }
    }

    public IReadOnlyList<AuditRecord> GetByAgent(string agentId, int count = 50)
    {
        lock (_lock)
        {
            return _buffer
                .Where(r => string.Equals(r.AgentId, agentId, StringComparison.OrdinalIgnoreCase))
                .Take(Math.Max(1, count))
                .ToList();
        }
    }

    public static string HashPrompt(string text)
    {
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        var rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            Span<byte> hashBuffer = stackalloc byte[32];
            SHA256.HashData(rented.AsSpan(0, written), hashBuffer);
            return Convert.ToHexStringLower(hashBuffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
