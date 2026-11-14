namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// Broadcasts health and incident changes from <see cref="MonitoringWorker"/> to every connected SSE client.
/// </summary>
public interface IVigilFeed
{
    void Publish(VigilFeedEvent evt);

    /// <summary>
    /// Starts receiving updates published from now on; dispose the subscription to stop.
    /// </summary>
    VigilFeedSubscription Subscribe();
}
