using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Execution;

/// <summary>
/// Executes code inside a security-hardened isolated container.
/// Implementations are responsible for container lifecycle, resource limits,
/// volume mapping, and post-execution cleanup.
/// </summary>
public interface ISandboxRunner
{
    /// <summary>
    /// Runs the requested code inside an isolated container and returns the execution result.
    /// The container is always removed on return, even when execution fails or times out.
    /// </summary>
    Task<ExecutionResult> RunAsync(ExecutionRequest request, CancellationToken ct);
}
