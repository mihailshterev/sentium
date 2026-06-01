using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Sentium.Infrastructure.Extensions;

public static class ResilienceExtensions
{
    /// <summary>
    /// Replaces the globally-applied standard resilience handler with one tuned for long-running calls
    /// (LLM token generation, sandboxed code execution) whose duration far exceeds the 30s default
    /// <c>TotalRequestTimeout</c>.
    /// <para>
    /// When <paramref name="retries"/> is 0, <see cref="AddStandardResilienceHandler"/> cannot be used
    /// because it enforces <c>MaxRetryAttempts >= 1</c> at startup validation. A custom pipeline is
    /// built instead: total-timeout → circuit-breaker → attempt-timeout, with no retry stage.
    /// This is correct for non-idempotent operations (LLM streaming, code execution).
    /// </para>
    /// </summary>
    public static IHttpClientBuilder AddLongRunningResilienceHandler(
        this IHttpClientBuilder builder,
        TimeSpan totalTimeout,
        TimeSpan attemptTimeout,
        int retries)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var minSampling = TimeSpan.FromTicks(Math.Max((attemptTimeout * 2).Ticks, totalTimeout.Ticks));
        var samplingDuration = minSampling + TimeSpan.FromMinutes(1);

#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        if (retries <= 0)
        {
            builder.AddResilienceHandler("long-running-no-retry", b =>
            {
                b.AddTimeout(totalTimeout);
                b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = samplingDuration,
                });
                b.AddTimeout(attemptTimeout);
            });
        }
        else
        {
            builder.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = totalTimeout;
                options.AttemptTimeout.Timeout = attemptTimeout;
                options.Retry.MaxRetryAttempts = retries;
                options.CircuitBreaker.SamplingDuration = samplingDuration;
            });
        }

        return builder;
    }
}
