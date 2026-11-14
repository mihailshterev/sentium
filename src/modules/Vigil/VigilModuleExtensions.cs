using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Metrics;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Monitoring.Probes;
using Sentium.Vigil.Features.Settings;

namespace Sentium.Vigil;

public static class VigilModuleExtensions
{
    /// <summary>
    /// Modules hosted in Sentium.Web, probed in-process through the health checks each one tags with its name.
    /// </summary>
    private static readonly (string Display, string Tag)[] MonitoredModules =
    [
        ("Identity", "identity"),
        ("Sentinel", "sentinel"),
        ("Praxis",   "praxis"),
        ("MCP",      "mcp"),
        ("Codex",    "codex"),
        ("Locus",    "locus"),
        ("Tempus",   "tempus"),
        ("Nuntius",  "nuntius"),
        ("Registry", "registry")
    ];

    /// <summary>
    /// Processes outside Sentium.Web, probed over HTTP on their <c>/health</c> endpoint.
    /// </summary>
    private static readonly (string Display, string ClientName)[] MonitoredServices =
    [
        ("Sandbox", ServiceNames.Sandbox)
    ];

    public static IHostApplicationBuilder AddVigilModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddSingleton<IVigilSettingsProvider, RegistryVigilSettingsProvider>();

        services.AddSingleton<IVigil, VigilService>();
        services.AddSingleton<IServiceHealthStateStore, ServiceHealthStateStore>();
        services.AddSingleton<IIncidentStore, IncidentStore>();
        services.AddSingleton<IVigilFeed, VigilFeed>();

        RegisterModuleProbes(services);
        RegisterServiceProbes(services);
        RegisterInfrastructureProbes(services, builder.Configuration);

        services.AddHostedService<MonitoringWorker>();

        return builder;
    }

    private static void RegisterModuleProbes(IServiceCollection services)
    {
        foreach (var (display, tag) in MonitoredModules)
        {
            services.AddSingleton<IHealthProbe>(sp => new ModuleHealthProbe(sp.GetRequiredService<HealthCheckService>(), display, tag));
        }
    }

    private static void RegisterServiceProbes(IServiceCollection services)
    {
        foreach (var (display, clientName) in MonitoredServices)
        {
#pragma warning disable EXTEXP0001
            services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri($"https+http://{clientName}");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .RemoveAllResilienceHandlers()
            .RemoveAllLoggers();
#pragma warning restore EXTEXP0001

            services.AddSingleton<IHealthProbe>(sp => new HttpHealthProbe(sp.GetRequiredService<IHttpClientFactory>(), display, clientName));
        }
    }

    private static void RegisterInfrastructureProbes(IServiceCollection services, IConfiguration configuration)
    {
        AddTcpProbe(services, configuration, ResourceNames.Redis, "Redis", defaultPort: 6379);
        AddTcpProbe(services, configuration, ResourceNames.Postgres, "PostgreSQL", defaultPort: 5432);

        // Both llama-server instances expose an unauthenticated /health that only turns healthy once their
        // model is loaded, so a loading or crashed model shows up here, not just a dead process.
        var inference = configuration.GetSection(InferenceOptions.SectionName).Get<InferenceOptions>() ?? new InferenceOptions();
        AddHttpInfraProbe(services, "LLM", $"{ResourceNames.Llm}-probe", inference.ChatEndpoint);
        AddHttpInfraProbe(services, "Embeddings", $"{ResourceNames.Embeddings}-probe", inference.EmbeddingEndpoint);
    }

    private static void AddHttpInfraProbe(IServiceCollection services, string target, string clientName, Uri baseAddress)
    {
#pragma warning disable EXTEXP0001
        services.AddHttpClient(clientName, client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .RemoveAllResilienceHandlers()
        .RemoveAllLoggers();
#pragma warning restore EXTEXP0001

        services.AddSingleton<IHealthProbe>(sp => new HttpInfraHealthProbe(sp.GetRequiredService<IHttpClientFactory>(), clientName, target, "health"));
    }

    private static void AddTcpProbe(IServiceCollection services, IConfiguration configuration, string connectionName, string target, int defaultPort)
    {
        var endpoint = EndpointResolver.ResolveHostPort(configuration.GetConnectionString(connectionName), defaultPort);
        if (endpoint is null)
        {
            return;
        }

        services.AddSingleton<IHealthProbe>(new TcpHealthProbe(target, endpoint.Value.Host, endpoint.Value.Port));
    }
}
