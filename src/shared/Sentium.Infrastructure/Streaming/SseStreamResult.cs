using Microsoft.AspNetCore.Http;
using Sentium.Core.Constants;

namespace Sentium.Infrastructure.Streaming;

/// <summary>
/// Adds <c>X-Accel-Buffering: no</c> to the framework's SSE result, so a buffering reverse proxy in front of
/// Sentium passes each event straight through.
/// </summary>
internal sealed class SseStreamResult(IResult inner) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers[CommonHeaderNames.AccelBuffering] = "no";
        return inner.ExecuteAsync(httpContext);
    }
}
