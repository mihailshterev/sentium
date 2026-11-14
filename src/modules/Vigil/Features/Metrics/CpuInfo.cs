namespace Sentium.Vigil.Features.Metrics;

internal sealed record CpuInfo
{
    public int ProcessorCount { get; init; }
    public double ProcessCpuPercent { get; init; }
    public required string Architecture { get; init; }
}
