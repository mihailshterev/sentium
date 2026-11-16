using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// What the Sandbox's <c>POST /execute</c> returns. The policy outcome is not part of it: Praxis authorizes the
/// run with Sentinel before calling the Sandbox and adds the decision to <see cref="SandboxExecutionResponse"/>.
/// </summary>
internal sealed record SandboxRunResponse
{
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public required Guid JobId { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<ArtifactDto> Artifacts { get; init; } = [];
}
