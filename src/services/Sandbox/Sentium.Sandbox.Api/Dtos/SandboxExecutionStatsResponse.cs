namespace Sentium.Sandbox.Api.Dtos;

public sealed record SandboxExecutionStatsResponse
{
    public required int Total { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required int Denied { get; init; }
}
