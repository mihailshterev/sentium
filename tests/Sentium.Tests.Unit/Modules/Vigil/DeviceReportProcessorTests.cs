using FluentAssertions;
using NSubstitute;
using Sentium.Nuntius.Contracts;
using Sentium.Vigil.Features.Devices;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class DeviceReportProcessorTests
{
    private readonly INotifier _notifier = Substitute.For<INotifier>();
    private readonly IVigilSettingsProvider _settings = Substitute.For<IVigilSettingsProvider>();
    private readonly VigilFeed _feed = new();
    private readonly DeviceStateStore _store = new(TimeProvider.System);
    private readonly DeviceReportProcessor _sut;
    private readonly Guid _device = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();

    public DeviceReportProcessorTests()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(new VigilRuntimeSettings { DeviceReportIntervalSeconds = 45 });
        _sut = new DeviceReportProcessor(_store, _settings, _feed, _notifier);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DeviceReport Report(params DeviceDiskReport[] disks)
        => new("DESKTOP-1", "Windows 11", 3_600, "1.0.0", 12.5, 16_000, 8_000, disks);

    [Fact]
    public async Task Process_ReturnsTheConfiguredInterval_AndStoresTheReport()
    {
        var response = await _sut.ProcessAsync(_device, _owner, Report(), Ct);

        response.NextReportSeconds.Should().Be(45);
        _store.Get(_device)!.Report.MachineName.Should().Be("DESKTOP-1");
    }

    [Fact]
    public async Task Process_PublishesTheUpdate_ForTheOwnerOnly()
    {
        using var subscription = _feed.Subscribe();

        await _sut.ProcessAsync(_device, _owner, Report(), Ct);

        subscription.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(VigilFeedEventTypes.DeviceUpdated);
        evt.AudienceUserId.Should().Be(_owner);
        evt.Data.Should().BeOfType<DeviceUpdatedFrame>().Which.Id.Should().Be(_device);
    }

    [Fact]
    public async Task FullDisk_NotifiesTheOwnerOnce_WithAKeyPerDeviceAndDisk()
    {
        var full = new DeviceDiskReport("C:", 1_000_000_000_000, 50_000_000_000);

        for (var i = 0; i < 5; i++)
        {
            await _sut.ProcessAsync(_device, _owner, Report(full), Ct);
        }

        await _notifier.Received(1).NotifyAsync(
            NotificationRecipient.User(_owner),
            Arg.Is<NotificationMessage>(m =>
                m.Category == NotificationCategories.DeviceDisk
                && m.Severity == NotificationSeverity.Warning
                && m.Title.Contains("C:") && m.Title.Contains("DESKTOP-1")
                && m.DedupeKey == $"device.disk:{_device}:C:"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthyDisks_NotifyNobody()
    {
        var healthy = new DeviceDiskReport("C:", 1_000, 600);

        for (var i = 0; i < 5; i++)
        {
            await _sut.ProcessAsync(_device, _owner, Report(healthy), Ct);
        }

        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default!, Ct);
    }
}
