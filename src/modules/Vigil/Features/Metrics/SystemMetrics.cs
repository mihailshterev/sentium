namespace Sentium.Vigil.Features.Metrics;

internal sealed record SystemMetrics
{
    public required HostInfo Host { get; init; }
    public required MemoryInfo Memory { get; init; }
    public required CpuInfo Cpu { get; init; }
    public required List<DiskInfo> Disks { get; init; }
    public required ProcessInfo Process { get; init; }
}
