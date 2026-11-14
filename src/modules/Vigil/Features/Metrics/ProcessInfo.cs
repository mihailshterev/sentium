namespace Sentium.Vigil.Features.Metrics;

internal sealed record ProcessInfo
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
