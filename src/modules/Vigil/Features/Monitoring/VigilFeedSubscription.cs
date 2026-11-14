using System.Threading.Channels;

namespace Sentium.Vigil.Features.Monitoring;

internal sealed class VigilFeedSubscription(ChannelReader<VigilFeedEvent> reader, Action unsubscribe) : IDisposable
{
    public ChannelReader<VigilFeedEvent> Reader { get; } = reader;

    public void Dispose() => unsubscribe();
}
