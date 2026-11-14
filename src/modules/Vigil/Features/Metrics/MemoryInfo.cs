namespace Sentium.Vigil.Features.Metrics;

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
