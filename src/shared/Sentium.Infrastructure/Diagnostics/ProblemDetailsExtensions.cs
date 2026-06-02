using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Infrastructure.Diagnostics;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Registers ProblemDetails generation and the shared <see cref="GlobalExceptionHandler"/>.
    /// The <c>CustomizeProblemDetails</c> callback runs for every ProblemDetails the framework
    /// produces (including 400 validation and 404 responses), so the telemetry traceId is always attached.
    /// </summary>
    public static IServiceCollection AddSentiumProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
