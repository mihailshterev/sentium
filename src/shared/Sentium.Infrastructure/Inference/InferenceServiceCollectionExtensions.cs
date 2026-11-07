using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Infrastructure.Inference;

public static class InferenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module-owned named HttpClient for one of the inference servers, with the long-running
    /// no-retry resilience pipeline, plus (once) the validated <see cref="InferenceOptions"/> and the
    /// <see cref="IInferenceClientFactory"/> that turns the client into chat or embedding clients.
    /// Each module keeps its own client name so their configurations never merge.
    /// </summary>
    /// <param name="totalTimeout">Overrides <see cref="InferenceOptions.Timeout"/> for this client.</param>
    /// <param name="attemptTimeout">Overrides <see cref="InferenceOptions.AttemptTimeout"/> for this client.</param>
    public static IHttpClientBuilder AddInferenceHttpClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string name,
        InferenceEndpoint endpoint,
        TimeSpan? totalTimeout = null,
        TimeSpan? attemptTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        AddInferenceCore(services, configuration);

        // The resilience pipeline is built at registration time, so its timeouts are read eagerly here;
        // the endpoint and API key are resolved from the validated options at runtime.
        var configured = configuration.GetSection(InferenceOptions.SectionName).Get<InferenceOptions>() ?? new InferenceOptions();
        var total = totalTimeout ?? configured.Timeout;
        var attempt = attemptTimeout ?? configured.AttemptTimeout;

        services.Configure<InferenceHttpClientOptions>(name, o =>
        {
            o.Endpoint = endpoint;
            o.NetworkTimeout = total;
        });

        return services.AddHttpClient(name, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
                client.BaseAddress = endpoint == InferenceEndpoint.Chat ? options.ChatEndpoint : options.EmbeddingEndpoint;
                client.Timeout = total;
            })
            .AddLongRunningResilienceHandler(totalTimeout: total, attemptTimeout: attempt, retries: 0);
    }

    private static void AddInferenceCore(IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(d => d.ServiceType == typeof(IInferenceClientFactory)))
        {
            return;
        }

        services.AddOptions<InferenceOptions>()
            .Bind(configuration.GetSection(InferenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IInferenceClientFactory, InferenceClientFactory>();
    }
}
