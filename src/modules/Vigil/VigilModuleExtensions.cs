using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Settings;
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
        ("Registry", "registry")
    ];

    /// <summary>
    /// Services that still run out of process, probed over HTTP on their <c>/health</c> endpoint.
    /// </summary>
    private static readonly (string Display, string ClientName)[] MonitoredServices =
    [
        ("Sandbox", ServiceNames.Sandbox)
    ];

    public static IHostApplicationBuilder AddVigilModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddSingleton<IWatchdogSettingsProvider, RegistryWatchdogSettingsProvider>();
        services.AddSingleton<ISettingsCacheInvalidationHandler, WatchdogSettingsCacheInvalidationHandler>();

        services.AddSingleton<IWatchdog, WatchdogService>();
        services.AddSingleton<IServiceHealthStateStore, ServiceHealthStateStore>();
        services.AddSingleton<IIncidentStore, IncidentStore>();
        services.AddSingleton<IWatchdogFeed, WatchdogFeed>();

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
        AddTcpProbe(services, configuration, ResourceNames.Qdrant, "Qdrant", defaultPort: 6334);

        var ollamaConn = configuration.GetConnectionString(ResourceNames.Ollama);
        var ollamaUri = EndpointResolver.ResolveUri(ollamaConn);
        if (ollamaUri is not null)
        {
            const string ollamaClient = "ollama-probe";
            services.AddHttpClient(ollamaClient, client => client.BaseAddress = ollamaUri);
            services.AddSingleton<IHealthProbe>(sp => new HttpInfraHealthProbe(sp.GetRequiredService<IHttpClientFactory>(), ollamaClient, "Ollama", "api/version"));
        }
        else
        {
            AddTcpProbe(services, configuration, ResourceNames.Ollama, "Ollama", defaultPort: 11434);
        }
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
