using System.Net;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// Dispatches code to the separate Sentium.Sandbox service and records every run (including
/// policy-denied ones) in the Praxis execution history.
/// </summary>
public interface ISandboxClient
{
    /// <summary>
    /// Executes <paramref name="request"/> in the sandbox.
    /// </summary>
    /// <exception cref="SandboxRejectedException">The sandbox refused the request (e.g. it failed validation).</exception>
    Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// The sandbox answered with a non-success status, so nothing ran and nothing was recorded.
/// </summary>
public sealed class SandboxRejectedException(HttpStatusCode statusCode, string details)
    : Exception($"Sandbox rejected the execution request ({(int)statusCode} {statusCode}): {details}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string Details { get; } = details;
}
