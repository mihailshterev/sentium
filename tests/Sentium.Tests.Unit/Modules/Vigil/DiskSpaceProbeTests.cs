using Microsoft.Extensions.Options;
using Sentium.Vigil.Features.Metrics;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Monitoring.Probes;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class DiskSpaceProbeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(50, "Healthy")]
    [InlineData(90, "Degraded")]
    [InlineData(94.9, "Degraded")]
    [InlineData(95, "Unhealthy")]
    public async Task FullestDisk_SetsTheStatus(double usedPercent, string expectedStatus)
    {
        var expected = Enum.Parse<ServiceStatus>(expectedStatus);
        var probe = Probe(Disk("/data", 10), Disk("/", usedPercent));

        var status = await probe.ProbeAsync(Ct);

        Assert.Equal(expected, status.Status);
        Assert.Equal(ComponentKind.Infrastructure, status.Kind);
        Assert.StartsWith("/ is ", status.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoVisibleDisks_IsHealthy()
    {
        var status = await Probe(Disk("/empty", 99, totalGb: 0)).ProbeAsync(Ct);

        Assert.Equal(ServiceStatus.Healthy, status.Status);
    }

    [Fact]
    public async Task Thresholds_ComeFromOptions()
    {
        var probe = new DiskSpaceProbe(() => [Disk("/", 75)], Options.Create(new DiskSpaceOptions { WarningPercent = 70, CriticalPercent = 80 }));

        Assert.Equal(ServiceStatus.Degraded, (await probe.ProbeAsync(Ct)).Status);
    }

    private static DiskSpaceProbe Probe(params DiskInfo[] disks) =>
        new(() => disks, Options.Create(new DiskSpaceOptions()));

    private static DiskInfo Disk(string name, double usedPercent, double totalGb = 100)
    {
        return new DiskInfo
        {
            Name = name,
            Label = name,
            FileSystem = "ext4",
            TotalGb = totalGb,
            UsedGb = totalGb * usedPercent / 100,
            AvailableGb = totalGb * (100 - usedPercent) / 100,
            UsagePercent = usedPercent
        };
    }
}
