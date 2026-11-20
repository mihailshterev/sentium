using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Infrastructure.Middleware;

namespace Sentium.Infrastructure.Extensions;

public static class LoggingExtensions
{
    private const string ConsoleTimestampFormat = "HH:mm:ss ";

    /// <summary>
    /// Writes timestamped console logs with the category on its own line and the message below it, for readability.
    /// Export to the OTLP collector comes from the OpenTelemetry logger provider that <c>AddServiceDefaults</c>
    /// registers, and the <c>Logging:LogLevel</c> configuration filters both.
    /// </summary>
    public static void AddSentiumAuditLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = false;
            options.TimestampFormat = ConsoleTimestampFormat;
        });
    }

    public static IApplicationBuilder UseSentiumTracing(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestTracingMiddleware>();
    }
}
