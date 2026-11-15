using Sentium.Identity.Contracts;

namespace Sentium.Vigil.Features.Devices;

/// <param name="LastSeenAt">
/// The time of the latest Report, or the persisted last-seen time when none has arrived since the server started.
/// </param>
/// <param name="Report">The latest Report; <see langword="null"/> until one arrives after a server restart.</param>
internal sealed record DeviceStatusDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string? OwnerUserName,
    string? AppVersion,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    DeviceReport? Report)
{
    public static DeviceStatusDto From(DeviceInfo device, DeviceSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceStatusDto(
            device.Id,
            device.Name,
            device.OwnerUserId,
            device.OwnerUserName,
            snapshot?.Report.AppVersion ?? device.AppVersion,
            snapshot?.IsOnline ?? false,
            snapshot?.ReportedAt ?? device.LastSeenAt,
            snapshot?.Report);
    }
}
