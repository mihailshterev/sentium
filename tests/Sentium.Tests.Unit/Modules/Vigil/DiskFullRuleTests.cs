using Sentium.Vigil.Features.Devices;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class DiskFullRuleTests
{
    private const long Total = 1_000;

    private readonly DiskFullRule _sut = new();

    private static DeviceDiskReport Disk(string name, int usedPercent) => new(name, Total, Total - (usedPercent * Total / 100));

    private IReadOnlyList<DeviceDiskReport> Report(params DeviceDiskReport[] disks) => _sut.Evaluate(disks);

    [Fact]
    public void FullDisk_IsReportedOnTheThirdConsecutiveReport_AndOnlyOnce()
    {
        Assert.Empty(Report(Disk("C:", 95)));
        Assert.Empty(Report(Disk("C:", 95)));
        Assert.Equal("C:", Assert.Single(Report(Disk("C:", 96))).Name);

        // the owner was already told
        Assert.Empty(Report(Disk("C:", 97)));
        Assert.Empty(Report(Disk("C:", 99)));
    }

    [Fact]
    public void DipUnderTheThreshold_RestartsTheCount()
    {
        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        Report(Disk("C:", 80));

        Assert.Empty(Report(Disk("C:", 95)));
        Assert.Empty(Report(Disk("C:", 95)));
        Assert.Single(Report(Disk("C:", 95)));
    }

    [Fact]
    public void RecoveredDisk_IsReportedAgain_WhenItFillsUpAgain()
    {
        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        Assert.Single(Report(Disk("C:", 95)));

        Assert.Empty(Report(Disk("C:", 50)));

        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        // it recovered in between, so this is a new problem
        Assert.Single(Report(Disk("C:", 95)));
    }

    [Fact]
    public void ExactlyAtTheThreshold_IsNotFull()
    {
        Report(Disk("C:", 90));
        Report(Disk("C:", 90));
        Assert.Empty(Report(Disk("C:", 90)));
    }

    [Fact]
    public void Disks_AreTrackedIndependently()
    {
        Report(Disk("C:", 95), Disk("D:", 10));
        Report(Disk("C:", 95), Disk("D:", 95));
        Assert.Equal(new string[] { "C:" }, Report(Disk("C:", 95), Disk("D:", 95)).Select(d => d.Name));
        Assert.Equal(new string[] { "D:" }, Report(Disk("C:", 95), Disk("D:", 95)).Select(d => d.Name));
    }

    [Fact]
    public void DiskThatDisappears_StartsOverWhenItReturns()
    {
        Report(Disk("E:", 95));
        Report(Disk("E:", 95));
        Report();

        Assert.Empty(Report(Disk("E:", 95)));
        Assert.Empty(Report(Disk("E:", 95)));
        Assert.Single(Report(Disk("E:", 95)));
    }

    [Fact]
    public void EmptyDisk_WithNoCapacity_IsNeverFull()
    {
        var unformatted = new DeviceDiskReport("F:", 0, 0);

        Report(unformatted);
        Report(unformatted);
        Assert.Empty(Report(unformatted));
    }
}
