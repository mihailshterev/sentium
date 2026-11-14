namespace Sentium.Praxis.Contracts;

/// <summary>
/// Dispatches code to the separate Sentium.Sandbox service and records every run (including
/// policy-denied ones) in the Praxis execution history.
/// </summary>
public interface ISandboxClient
{
    /// <exception cref="SandboxRejectedException">The sandbox refused the request (e.g. it failed validation).</exception>
    Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, string correlationId, CancellationToken ct = default);
}
