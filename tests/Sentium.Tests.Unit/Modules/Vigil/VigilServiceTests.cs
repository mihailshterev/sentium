using Sentium.Vigil.Features.Metrics;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class VigilServiceTests
{
    private readonly VigilService _service = new();

    [Fact]
    public void GetMetrics_ReturnsNonNullResult()
    {
        var metrics = _service.GetMetrics();

        Assert.NotNull(metrics);
    }

    [Fact]
    public void GetMetrics_HostInfo_IsPopulated()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            Assert.False(string.IsNullOrWhiteSpace(metrics.Host.MachineName));
            Assert.False(string.IsNullOrWhiteSpace(metrics.Host.OsDescription));
            Assert.False(string.IsNullOrWhiteSpace(metrics.Host.OsArchitecture));
            Assert.True(metrics.Host.ProcessorCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(metrics.Host.RuntimeVersion));
            Assert.True(metrics.Host.Uptime > TimeSpan.Zero);
        });
    }

    [Fact]
    public void GetMetrics_MemoryInfo_HasPositiveTotalMemory()
    {
        var metrics = _service.GetMetrics();

        Assert.True(metrics.Memory.TotalMb > 0);
    }

    [Fact]
    public void GetMetrics_CpuInfo_IsPopulated()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            Assert.True(metrics.Cpu.ProcessorCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(metrics.Cpu.Architecture));
        });
    }

    [Fact]
    public void GetMetrics_ProcessInfo_ReflectsCurrentProcess()
    {
        var metrics = _service.GetMetrics();

        Assert.Multiple(() =>
        {
            Assert.True(metrics.Process.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(metrics.Process.Name));
            Assert.True(metrics.Process.WorkingSetMb > 0);
        });
    }

    [Fact]
    public void GetMetrics_DisksInfo_HasAtLeastOneDrive()
    {
        var metrics = _service.GetMetrics();

        Assert.NotEmpty(metrics.Disks);
    }

    [Fact]
    public void GetMetrics_CalledTwice_ReturnsDifferentInstances()
    {
        var first = _service.GetMetrics();
        var second = _service.GetMetrics();

        Assert.NotSame(second, first);
    }
}
