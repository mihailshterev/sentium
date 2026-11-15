using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// A Device that stops reporting sends nothing, so the Offline transition cannot come from the report path. This
/// only pushes the change to the live stream; the list endpoint computes the state itself and is correct without it.
/// Going Offline never opens an Incident and never notifies anyone.
/// </summary>
internal sealed class DeviceOfflineWatcher(IDeviceStateStore stateStore, IVigilFeed feed) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                PublishNewlyOffline();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void PublishNewlyOffline()
    {
        foreach (var snapshot in stateStore.TakeNewlyOffline())
        {
            feed.Publish(new VigilFeedEvent(
                VigilFeedEventTypes.DeviceUpdated,
                DeviceUpdatedFrame.From(snapshot),
                AudienceUserId: snapshot.OwnerUserId));
        }
    }
}
