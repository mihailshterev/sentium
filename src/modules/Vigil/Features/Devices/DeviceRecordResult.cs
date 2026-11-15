namespace Sentium.Vigil.Features.Devices;

/// <param name="NewlyFullDisks">Disks the owner should be told about now; see <see cref="DiskFullRule"/>.</param>
internal sealed record DeviceRecordResult(DeviceSnapshot Snapshot, IReadOnlyList<DeviceDiskReport> NewlyFullDisks);
