using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Settings;
using Sentium.Infrastructure.Security;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Application.Monitoring.Probes;
using Sentium.Watchdog.Application.Settings;
using Sentium.Watchdog.Core.Metrics;
using Sentium.Watchdog.Core.Monitoring;
using Sentium.Watchdog.Core.Settings;

namespace Sentium.Watchdog.Application;

public static class ServiceCollectionExtensions
{
    private static readonly (string Display, string ClientName)[] MonitoredServices =
    [
        ("Identity",      ServiceNames.Identity),
        ("Sentinel",      ServiceNames.Sentinel),
        ("Agent Runtime", ServiceNames.AgentRuntime),
        ("Registry",      ServiceNames.Registry),
        ("Sandbox",       ServiceNames.Sandbox)
    ];

    public static IHostApplicationBuilder AddWatchdogApplication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        builder.AddInternalApiSecurity();
        services.AddHybridCache();
        services.AddTransient<InternalApiKeyDelegatingHandler>();

        services.AddHttpClient(ServiceNames.Registry, client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Registry}");
        }).AddHttpMessageHandler<InternalApiKeyDelegatingHandler>();

        services.AddSingleton<IWatchdogSettingsProvider, RegistryWatchdogSettingsProvider>();
        services.AddSingleton<ISettingsCacheInvalidationHandler, WatchdogSettingsCacheInvalidationHandler>();

        services.AddSingleton<IWatchdog, WatchdogService>();
        services.AddSingleton<IServiceHealthStateStore, ServiceHealthStateStore>();
        services.AddSingleton<IIncidentStore, IncidentStore>();
        services.AddSingleton<IEventBus, NatsEventBus>();

        RegisterServiceProbes(services);
        RegisterInfrastructureProbes(services, builder.Configuration);

        services.AddHostedService<MonitoringWorker>();
        builder.AddSettingsSyncWorker();

        return builder;
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
        services.AddSingleton<IHealthProbe>(sp => new NatsHealthProbe(sp.GetRequiredService<global::NATS.Client.Core.INatsConnection>()));

        AddTcpProbe(services, configuration, ResourceNames.Redis, "Redis", defaultPort: 6379);
        AddTcpProbe(services, configuration, ResourceNames.Sql, "SQL Server", defaultPort: 1433);
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
