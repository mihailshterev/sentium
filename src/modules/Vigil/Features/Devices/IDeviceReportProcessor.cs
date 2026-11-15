namespace Sentium.Vigil.Features.Devices;

internal interface IDeviceReportProcessor
{
    /// <summary>
    /// Records a Report, pushes it to the live stream of the Device's owner and the Sovereigns, and notifies the
    /// owner of a disk that has just become nearly full.
    /// </summary>
    Task<DeviceReportResponse> ProcessAsync(Guid deviceId, Guid ownerUserId, DeviceReport report, CancellationToken ct);
}
