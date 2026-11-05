using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Infrastructure.Extensions;

/// <summary>
/// Shared Ollama HTTP plumbing for the modules that talk to the local model server (Praxis agents, Codex embeddings,
/// Sentinel intent checks): base URL, timeouts, and a long-running, no-retry resilience pipeline.
/// </summary>
public static class OllamaExtensions
{
    private const string BaseUrlKey = "AI:OllamaBaseUrl";
    private const string TotalTimeoutKey = "AI:OllamaTimeout";
    private const string AttemptTimeoutKey = "AI:OllamaAttemptTimeout";

    private static readonly Uri DefaultBaseUrl = new("http://localhost:11434");
    private static readonly TimeSpan DefaultTotalTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromMinutes(10);

    public static Uri GetOllamaBaseUrl(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[BaseUrlKey] is { Length: > 0 } url ? new Uri(url) : DefaultBaseUrl;
    }

    /// <summary>
    /// Registers a named <see cref="HttpClient"/> for Ollama. Generation is non-idempotent and slow, so the pipeline has
    /// generous timeouts and no retries. Timeouts default to <c>AI:OllamaTimeout</c> / <c>AI:OllamaAttemptTimeout</c>.
    /// </summary>
    public static IHttpClientBuilder AddOllamaHttpClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string name,
        TimeSpan? totalTimeout = null,
        TimeSpan? attemptTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration.GetOllamaBaseUrl();
        var total = totalTimeout ?? configuration.GetTimeSpan(TotalTimeoutKey, DefaultTotalTimeout);
        var attempt = attemptTimeout ?? configuration.GetTimeSpan(AttemptTimeoutKey, DefaultAttemptTimeout);

        return services
            .AddHttpClient(name, client =>
            {
                client.BaseAddress = baseUrl;
                client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns timeouts
            })
            .AddLongRunningResilienceHandler(totalTimeout: total, attemptTimeout: attempt, retries: 0);
    }

    /// <summary>
    /// Reads a positive duration given either as a <see cref="TimeSpan"/> (<c>00:30:00</c>) or as a number of minutes.
    /// </summary>
    public static TimeSpan GetTimeSpan(this IConfiguration configuration, string key, TimeSpan fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (TimeSpan.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
        {
            return asTimeSpan;
        }

        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : fallback;
    }
}
