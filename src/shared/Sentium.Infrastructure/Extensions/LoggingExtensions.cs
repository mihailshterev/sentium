using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Middleware;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Serilog.Sinks.SystemConsole.Themes;

namespace Sentium.Infrastructure.Extensions;

public static class LoggingExtensions
{
    public static void AddSentiumAuditLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        builder.Services.AddSerilog((sp, cfg) =>
        {
            cfg.Enrich.FromLogContext()
               .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
               .WriteTo.Console(theme: AnsiConsoleTheme.Code);

            if (string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                return;
            }

            cfg.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
                options.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField;

                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = builder.Environment.ApplicationName
                };
            });
        });
    }

    public static IApplicationBuilder UseSentiumTracing(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestTracingMiddleware>();
    }
}
