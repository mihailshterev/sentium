using System.Globalization;
using Sentium.Nuntius.Contracts;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Settings;

namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// Devices are not probes: nothing here touches the incident store. A nearly full disk goes to the Device's owner as
/// a Notification, because the owner is the one who can free the space.
/// </summary>
internal sealed class DeviceReportProcessor(
    IDeviceStateStore stateStore,
    IVigilSettingsProvider settingsProvider,
    IVigilFeed feed,
    INotifier notifier) : IDeviceReportProcessor
{
    private const string DevicesLink = "/vigil";
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    public async Task<DeviceReportResponse> ProcessAsync(Guid deviceId, Guid ownerUserId, DeviceReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);

        var settings = await settingsProvider.GetAsync(ct);
        var result = stateStore.Record(deviceId, ownerUserId, report, settings.DeviceReportIntervalSeconds);

        feed.Publish(new VigilFeedEvent(
            VigilFeedEventTypes.DeviceUpdated,
            DeviceUpdatedFrame.From(result.Snapshot),
            AudienceUserId: ownerUserId));

        foreach (var disk in result.NewlyFullDisks)
        {
            await notifier.NotifyAsync(NotificationRecipient.User(ownerUserId), DiskFullMessage(deviceId, report, disk), ct);
        }

        return new DeviceReportResponse(settings.DeviceReportIntervalSeconds);
    }

    private static NotificationMessage DiskFullMessage(Guid deviceId, DeviceReport report, DeviceDiskReport disk)
    {
        var body = string.Create(
            CultureInfo.InvariantCulture,
            $"{disk.UsedPercent:0}% used, {disk.AvailableBytes / BytesPerGb:0.#} GB free.");

        return new NotificationMessage(
            NotificationCategories.DeviceDisk,
            NotificationSeverity.Warning,
            $"Disk {disk.Name} on {report.MachineName} is almost full",
            body,
            DevicesLink,
            $"{NotificationCategories.DeviceDisk}:{deviceId}:{disk.Name}");
    }
}
