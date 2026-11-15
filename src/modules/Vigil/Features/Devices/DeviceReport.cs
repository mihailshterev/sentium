namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// One periodic snapshot of a Device's health, sent by the Satelles app. This is everything a Device ever tells the
/// server about its PC: nothing here describes what the person is doing. The Satelles app keeps its own copy of this
/// shape, so a rename is a breaking change for paired Devices.
/// </summary>
/// <param name="CpuPercent">System-wide CPU use since the previous Report, 0 to 100.</param>
/// <param name="Disks">Fixed disks only.</param>
internal sealed record DeviceReport(
    string MachineName,
    string OsDescription,
    long UptimeSeconds,
    string AppVersion,
    double CpuPercent,
    long MemoryTotalBytes,
    long MemoryAvailableBytes,
    IReadOnlyList<DeviceDiskReport> Disks);
