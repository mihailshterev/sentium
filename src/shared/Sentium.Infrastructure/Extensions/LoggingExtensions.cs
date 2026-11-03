using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Middleware;
using Sentium.Core.Constants;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Serilog.Sinks.SystemConsole.Themes;

namespace Sentium.Infrastructure.Extensions;

public static class LoggingExtensions
{
    public static void AddSentiumAuditLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.AddSeqEndpoint(ResourceNames.Seq);

        builder.Services.AddSerilog((sp, cfg) =>
        {
            cfg.Enrich.FromLogContext()
               .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
               .WriteTo.Console(theme: AnsiConsoleTheme.Code)
               .WriteTo.Seq(builder.Configuration.GetConnectionString(ResourceNames.Seq) ?? "http://localhost:5341")
               .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
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
