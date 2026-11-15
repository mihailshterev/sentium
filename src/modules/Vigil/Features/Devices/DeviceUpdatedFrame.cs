namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// Stream payload for a Device that reported or went Offline. It carries only what changes between Reports; the
/// portal patches the Device it already listed and refetches the list when it does not know the id.
/// </summary>
internal sealed record DeviceUpdatedFrame(Guid Id, bool IsOnline, DateTimeOffset LastSeenAt, DeviceReport Report)
{
    public static DeviceUpdatedFrame From(DeviceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new DeviceUpdatedFrame(snapshot.DeviceId, snapshot.IsOnline, snapshot.ReportedAt, snapshot.Report);
    }
}
