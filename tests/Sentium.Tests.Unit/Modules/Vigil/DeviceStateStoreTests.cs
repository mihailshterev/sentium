using Sentium.Vigil.Features.Devices;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class DeviceStateStoreTests
{
    private const int IntervalSeconds = 30;

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly DeviceStateStore _sut;
    private readonly Guid _device = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();

    public DeviceStateStoreTests()
    {
        _sut = new DeviceStateStore(_clock);
    }

    private static DeviceReport Report(double cpu = 10, params DeviceDiskReport[] disks)
        => new("DESKTOP-1", "Windows 11", 3_600, "1.0.0", cpu, 16_000, 8_000, disks);

    [Fact]
    public void Record_MakesTheDeviceOnline_WithItsLatestReport()
    {
        _sut.Record(_device, _owner, Report(cpu: 10), IntervalSeconds);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var result = _sut.Record(_device, _owner, Report(cpu: 42), IntervalSeconds);

        Assert.True(result.Snapshot.IsOnline);

        var snapshot = _sut.Get(_device)!;
        Assert.True(snapshot.IsOnline);
        Assert.Equal(_owner, snapshot.OwnerUserId);
        Assert.Equal(42, snapshot.Report.CpuPercent);
        Assert.Equal(_clock.GetUtcNow(), snapshot.ReportedAt);
    }

    [Fact]
    public void Device_StaysOnline_WhileFewerThanThreeReportsAreMissed()
    {
        _sut.Record(_device, _owner, Report(), IntervalSeconds);

        _clock.Advance(TimeSpan.FromSeconds(IntervalSeconds * 3));

        Assert.True(_sut.Get(_device)!.IsOnline);
        Assert.Empty(_sut.TakeNewlyOffline());
    }

    [Fact]
    public void Device_GoesOffline_AfterThreeMissedReports_AndKeepsItsLastSeenTime()
    {
        _sut.Record(_device, _owner, Report(), IntervalSeconds);
        var reportedAt = _clock.GetUtcNow();

        _clock.Advance(TimeSpan.FromSeconds((IntervalSeconds * 3) + 6));

        var snapshot = _sut.Get(_device)!;
        Assert.False(snapshot.IsOnline);
        Assert.Equal(reportedAt, snapshot.ReportedAt);
    }

    [Fact]
    public void TakeNewlyOffline_ReturnsEachTransitionOnce_AndAgainAfterTheDeviceCameBack()
    {
        _sut.Record(_device, _owner, Report(), IntervalSeconds);
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(_device, Assert.Single(_sut.TakeNewlyOffline()).DeviceId);
        Assert.Empty(_sut.TakeNewlyOffline());

        _sut.Record(_device, _owner, Report(), IntervalSeconds);
        Assert.Empty(_sut.TakeNewlyOffline());

        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Single(_sut.TakeNewlyOffline());
    }

    [Fact]
    public void OfflineThreshold_FollowsTheIntervalTheDeviceWasGiven()
    {
        _sut.Record(_device, _owner, Report(), reportIntervalSeconds: 300);

        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.True(_sut.Get(_device)!.IsOnline, "three 5-minute Reports are not overdue yet");
    }

    [Fact]
    public void Record_ReportsADiskOnceItHasBeenFullForThreeReports()
    {
        var full = new DeviceDiskReport("C:", 1_000, 50);

        Assert.Empty(_sut.Record(_device, _owner, Report(disks: full), IntervalSeconds).NewlyFullDisks);
        Assert.Empty(_sut.Record(_device, _owner, Report(disks: full), IntervalSeconds).NewlyFullDisks);
        Assert.Single(_sut.Record(_device, _owner, Report(disks: full), IntervalSeconds).NewlyFullDisks);
        Assert.Empty(_sut.Record(_device, _owner, Report(disks: full), IntervalSeconds).NewlyFullDisks);
    }

    [Fact]
    public void GetSamples_ReturnsTheMostRecent_OldestFirst()
    {
        foreach (var cpu in new[] { 1d, 2d, 3d })
        {
            _sut.Record(_device, _owner, Report(cpu), IntervalSeconds);
            _clock.Advance(TimeSpan.FromSeconds(IntervalSeconds));
        }

        var samples = _sut.GetSamples(_device, take: 2);

        Assert.Equal(new double[] { 2, 3 }, samples.Select(s => s.CpuPercent));
        Assert.Equal(50, samples[0].MemoryUsedPercent);
    }

    [Fact]
    public void Remove_DropsTheReportsAndState()
    {
        _sut.Record(_device, _owner, Report(), IntervalSeconds);

        Assert.True(_sut.Remove(_device));

        Assert.Null(_sut.Get(_device));
        Assert.Empty(_sut.GetSamples(_device, 10));
        Assert.False(_sut.Remove(_device));
    }

    [Fact]
    public void UnknownDevice_HasNoState()
    {
        Assert.Null(_sut.Get(Guid.NewGuid()));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
