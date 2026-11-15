using Sentium.Identity.Contracts;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// Removing a Device drops its Reports and state, and tells open Vigil pages to drop its row.
/// </summary>
internal sealed class DeviceStateRemover(IDeviceStateStore stateStore, IVigilFeed feed) : IDeviceRemovalObserver
{
    public void OnDeviceRemoved(Guid deviceId, Guid ownerUserId)
    {
        stateStore.Remove(deviceId);

        feed.Publish(new VigilFeedEvent(
            VigilFeedEventTypes.DeviceRemoved,
            new DeviceRemovedFrame(deviceId),
            AudienceUserId: ownerUserId));
    }
}
