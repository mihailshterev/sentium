namespace Sentium.Watchdog.Core.Metrics;

public sealed record SystemMetrics
{
    public required HostInfo Host { get; init; }
    public required MemoryInfo Memory { get; init; }
    public required CpuInfo Cpu { get; init; }
    public required List<DiskInfo> Disks { get; init; }
    public required ProcessInfo Process { get; init; }
}

public sealed record HostInfo
{
    public required string MachineName { get; init; }
    public required string OsDescription { get; init; }
    public required string OsArchitecture { get; init; }
    public required int ProcessorCount { get; init; }
    public required string RuntimeVersion { get; init; }
    public required TimeSpan Uptime { get; init; }
}

public sealed record MemoryInfo
{
    public double TotalMb { get; init; }
    public double UsedMb { get; init; }
    public double AvailableMb { get; init; }
    public double MemoryLoadPercent { get; init; }
    public double GcHeapSizeMb { get; init; }
    public int GcGen0Collections { get; init; }
    public int GcGen1Collections { get; init; }
    public int GcGen2Collections { get; init; }
}

public sealed record CpuInfo
{
    public int ProcessorCount { get; init; }
    public double ProcessCpuPercent { get; init; }
    public required string Architecture { get; init; }
}

public sealed record DiskInfo
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string FileSystem { get; init; }
    public double TotalGb { get; init; }
    public double AvailableGb { get; init; }
    public double UsedGb { get; init; }
    public double UsagePercent { get; init; }
}

public sealed record ProcessInfo
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public double WorkingSetMb { get; init; }
    public double PrivateMemoryMb { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan CpuTime { get; init; }
}
