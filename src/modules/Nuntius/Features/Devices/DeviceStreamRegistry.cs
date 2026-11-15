using Sentium.Identity.Contracts;

namespace Sentium.Nuntius.Features.Devices;

/// <summary>
/// Tracks the open notification streams of Devices so they can be closed the moment a Device is removed. A stream is
/// authenticated only when it opens; without this, a removed Device would keep receiving its former owner's
/// Notifications until it happened to reconnect.
/// </summary>
internal sealed class DeviceStreamRegistry : IDeviceRemovalObserver
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, List<CancellationTokenSource>> _streams = [];

    /// <summary>
    /// Registers an open stream. The returned lease's token fires when the Device is removed or
    /// <paramref name="requestAborted"/> does; dispose the lease when the stream ends.
    /// </summary>
    public DeviceStreamLease Open(Guid deviceId, CancellationToken requestAborted)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);

        lock (_gate)
        {
            if (!_streams.TryGetValue(deviceId, out var streams))
            {
                streams = [];
                _streams[deviceId] = streams;
            }

            streams.Add(cts);
        }

        return new DeviceStreamLease(cts, () => Close(deviceId, cts));
    }

    public void OnDeviceRemoved(Guid deviceId, Guid ownerUserId)
    {
        List<CancellationTokenSource>? streams;
        lock (_gate)
        {
            _streams.Remove(deviceId, out streams);
        }

        foreach (var stream in streams ?? [])
        {
            try
            {
                stream.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The stream ended on its own between the lookup and the cancel.
            }
        }
    }

    private void Close(Guid deviceId, CancellationTokenSource cts)
    {
        lock (_gate)
        {
            if (_streams.TryGetValue(deviceId, out var streams) && streams.Remove(cts) && streams.Count == 0)
            {
                _streams.Remove(deviceId);
            }
        }

        cts.Dispose();
    }
}
