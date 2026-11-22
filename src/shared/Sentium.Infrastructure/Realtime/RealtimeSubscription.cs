using System.Threading.Channels;

namespace Sentium.Infrastructure.Realtime;

/// <summary>
/// One open portal session: serialized frames for <see cref="UserId"/>, in a small bounded queue that drops its oldest
/// frames rather than slowing the publisher. Dispose to stop receiving.
/// </summary>
internal sealed class RealtimeSubscription : IDisposable
{
    private readonly Channel<string> channel;
    private readonly Action<RealtimeSubscription> unsubscribe;

    public RealtimeSubscription(Guid userId, bool isSovereign, int capacity, Action<RealtimeSubscription> unsubscribe)
    {
        UserId = userId;
        IsSovereign = isSovereign;
        this.unsubscribe = unsubscribe;
        channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Guid UserId { get; }

    public bool IsSovereign { get; }

    public ChannelReader<string> Reader => channel.Reader;

    public void Dispose()
    {
        unsubscribe(this);
        channel.Writer.TryComplete();
    }

    internal bool TryWrite(string frame) => channel.Writer.TryWrite(frame);
}
