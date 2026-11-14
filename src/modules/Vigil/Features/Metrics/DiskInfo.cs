namespace Sentium.Vigil.Features.Metrics;

internal sealed record DiskInfo
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string FileSystem { get; init; }
    public double TotalGb { get; init; }
    public double AvailableGb { get; init; }
    public double UsedGb { get; init; }
    public double UsagePercent { get; init; }
}
