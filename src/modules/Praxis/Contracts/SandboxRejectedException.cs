using System.Net;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// The sandbox answered with a non-success status, so nothing ran and nothing was recorded.
/// </summary>
public sealed class SandboxRejectedException(HttpStatusCode statusCode, string details)
    : Exception($"Sandbox rejected the execution request ({(int)statusCode} {statusCode}): {details}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string Details { get; } = details;
}
