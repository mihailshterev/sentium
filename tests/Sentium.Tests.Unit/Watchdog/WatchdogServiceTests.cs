using FluentAssertions;
using Sentium.Watchdog.Application;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

public sealed class WatchdogServiceTests
{
    private readonly WatchdogService _service = new();

    [Fact]
    public void GetMetrics_ReturnsNonNullResult()
    {
        var metrics = _service.GetMetrics();

        metrics.Should().NotBeNull();
    }

    [Fact]
    public void GetMetrics_HostInfo_IsPopulated()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            metrics.Host.MachineName.Should().NotBeNullOrWhiteSpace();
            metrics.Host.OsDescription.Should().NotBeNullOrWhiteSpace();
            metrics.Host.OsArchitecture.Should().NotBeNullOrWhiteSpace();
            metrics.Host.ProcessorCount.Should().BeGreaterThan(0);
            metrics.Host.RuntimeVersion.Should().NotBeNullOrWhiteSpace();
            metrics.Host.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
        });
    }

    [Fact]
    public void GetMetrics_MemoryInfo_HasPositiveTotalMemory()
    {
        var metrics = _service.GetMetrics();

        metrics.Memory.TotalMb.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetMetrics_CpuInfo_IsPopulated()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            metrics.Cpu.ProcessorCount.Should().BeGreaterThan(0);
            metrics.Cpu.Architecture.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void GetMetrics_ProcessInfo_ReflectsCurrentProcess()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            metrics.Process.Id.Should().BeGreaterThan(0);
            metrics.Process.Name.Should().NotBeNullOrWhiteSpace();
            metrics.Process.WorkingSetMb.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void GetMetrics_DisksInfo_HasAtLeastOneDrive()
    {
        var metrics = _service.GetMetrics();

        metrics.Disks.Should().NotBeEmpty();
    }

    [Fact]
    public void GetMetrics_CalledTwice_ReturnsDifferentInstances()
    {
        var first = _service.GetMetrics();
        var second = _service.GetMetrics();

        first.Should().NotBeSameAs(second);
    }
}
