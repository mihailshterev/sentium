using FluentAssertions;
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
        Report(Disk("C:", 95)).Should().BeEmpty();
        Report(Disk("C:", 95)).Should().BeEmpty();
        Report(Disk("C:", 96)).Should().ContainSingle().Which.Name.Should().Be("C:");

        Report(Disk("C:", 97)).Should().BeEmpty("the owner was already told");
        Report(Disk("C:", 99)).Should().BeEmpty();
    }

    [Fact]
    public void DipUnderTheThreshold_RestartsTheCount()
    {
        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        Report(Disk("C:", 80));

        Report(Disk("C:", 95)).Should().BeEmpty();
        Report(Disk("C:", 95)).Should().BeEmpty();
        Report(Disk("C:", 95)).Should().ContainSingle();
    }

    [Fact]
    public void RecoveredDisk_IsReportedAgain_WhenItFillsUpAgain()
    {
        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        Report(Disk("C:", 95)).Should().ContainSingle();

        Report(Disk("C:", 50)).Should().BeEmpty();

        Report(Disk("C:", 95));
        Report(Disk("C:", 95));
        Report(Disk("C:", 95)).Should().ContainSingle("it recovered in between, so this is a new problem");
    }

    [Fact]
    public void ExactlyAtTheThreshold_IsNotFull()
    {
        Report(Disk("C:", 90));
        Report(Disk("C:", 90));
        Report(Disk("C:", 90)).Should().BeEmpty();
    }

    [Fact]
    public void Disks_AreTrackedIndependently()
    {
        Report(Disk("C:", 95), Disk("D:", 10));
        Report(Disk("C:", 95), Disk("D:", 95));
        Report(Disk("C:", 95), Disk("D:", 95)).Select(d => d.Name).Should().Equal("C:");
        Report(Disk("C:", 95), Disk("D:", 95)).Select(d => d.Name).Should().Equal("D:");
    }

    [Fact]
    public void DiskThatDisappears_StartsOverWhenItReturns()
    {
        Report(Disk("E:", 95));
        Report(Disk("E:", 95));
        Report();

        Report(Disk("E:", 95)).Should().BeEmpty();
        Report(Disk("E:", 95)).Should().BeEmpty();
        Report(Disk("E:", 95)).Should().ContainSingle();
    }

    [Fact]
    public void EmptyDisk_WithNoCapacity_IsNeverFull()
    {
        var unformatted = new DeviceDiskReport("F:", 0, 0);

        Report(unformatted);
        Report(unformatted);
        Report(unformatted).Should().BeEmpty();
    }
}
