using Microsoft.AspNetCore.Http;
using Sentium.Shared.Constants;
using Serilog.Context;

namespace Sentium.Infrastructure.Middleware;

public class RequestTracingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        if (!context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var correlationId))
        {
            correlationId = context.TraceIdentifier;
        }

        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;

        using (LogContext.PushProperty(HeaderNames.CorrelationId, correlationId))
        {
            await next(context);
        }
    }
}
