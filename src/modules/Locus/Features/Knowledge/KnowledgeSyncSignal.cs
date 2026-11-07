using System.Threading.Channels;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Doorbell that wakes <see cref="KnowledgeSyncWorker"/> after a committed change queued work. It carries
/// no data - the outbox table is the source of truth - so a single pending ring is enough and extra rings
/// are dropped.
/// </summary>
public sealed class KnowledgeSyncSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>
    /// Completes when rung or after <paramref name="timeout"/>, whichever comes first.
    /// </summary>
    public async Task WaitAsync(TimeSpan timeout, TimeProvider timeProvider, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        using var timeoutCts = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await _channel.Reader.ReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timed out - time to look for retries that became due.
        }
    }
}
