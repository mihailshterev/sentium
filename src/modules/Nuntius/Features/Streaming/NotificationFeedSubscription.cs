using System.Threading.Channels;

namespace Sentium.Nuntius.Features.Streaming;

internal sealed class NotificationFeedSubscription(ChannelReader<NotificationFeedEvent> reader, Action unsubscribe) : IDisposable
{
    public ChannelReader<NotificationFeedEvent> Reader { get; } = reader;

    public void Dispose() => unsubscribe();
}
