namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// In-memory store of each Device's latest Report and a short sample history. Nothing is persisted: after a server
/// restart a Device is unknown here until its next Report arrives.
/// </summary>
internal interface IDeviceStateStore
{
    /// <param name="reportIntervalSeconds">
    /// The interval the Device was told to wait before its next Report; it decides when the Device counts as Offline.
    /// </param>
    DeviceRecordResult Record(Guid deviceId, Guid ownerUserId, DeviceReport report, int reportIntervalSeconds);

    DeviceSnapshot? Get(Guid deviceId);

    /// <summary>
    /// Returns up to <paramref name="take"/> most-recent samples, oldest first.
    /// </summary>
    IReadOnlyList<DeviceSample> GetSamples(Guid deviceId, int take);

    /// <summary>
    /// The Devices that have gone Offline since the last call. Each transition is returned exactly once.
    /// </summary>
    IReadOnlyList<DeviceSnapshot> TakeNewlyOffline();

    bool Remove(Guid deviceId);
}
