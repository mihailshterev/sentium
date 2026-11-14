namespace Sentium.Vigil.Features.Metrics;

public sealed record HostInfo
{
    public required string MachineName { get; init; }
    public required string OsDescription { get; init; }
    public required string OsArchitecture { get; init; }
    public required int ProcessorCount { get; init; }
    public required string RuntimeVersion { get; init; }
    public required TimeSpan Uptime { get; init; }
}
