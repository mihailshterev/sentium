using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sentium.Core.Constants;

namespace Sentium.Infrastructure.Middleware;

/// <summary>
/// Echoes the caller's correlation id (or the trace identifier) and puts it in a logging scope, so every log entry
/// written while handling the request carries it; the OpenTelemetry exporter sends scope values as attributes.
/// </summary>
internal class RequestTracingMiddleware(RequestDelegate next, ILogger<RequestTracingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        if (!context.Request.Headers.TryGetValue(CommonHeaderNames.CorrelationId, out var correlationId))
        {
            correlationId = context.TraceIdentifier;
        }

        context.Response.Headers[CommonHeaderNames.CorrelationId] = correlationId;

        var scope = new Dictionary<string, object> { [CommonHeaderNames.CorrelationId] = correlationId.ToString() };

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }
}
