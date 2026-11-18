using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sentium.Core.Constants;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Inference;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Speech;
using Sentium.Vigil.Features.Backups;
using Sentium.Vigil.Features.Devices;
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
        ("Memoria",  "memoria"),
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

        services.TryAddSingleton(TimeProvider.System);
        RegisterBackupMonitoring(services, builder.Configuration);

        services.AddHostedService<MonitoringWorker>();

        services.AddSingleton<IDeviceStateStore, DeviceStateStore>();
        services.AddSingleton<IDeviceReportProcessor, DeviceReportProcessor>();
        services.AddSingleton<IDeviceRemovalObserver, DeviceStateRemover>();
        services.AddHostedService<DeviceOfflineWatcher>();

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
            .RemoveAllLoggers();
#pragma warning restore EXTEXP0001

            services.AddSingleton<IHealthProbe>(sp => new HttpHealthProbe(sp.GetRequiredService<IHttpClientFactory>(), display, clientName));
        }
    }

    private static void RegisterInfrastructureProbes(IServiceCollection services, IConfiguration configuration)
    {
        AddTcpProbe(services, configuration, ResourceNames.SentiumDb, "PostgreSQL", defaultPort: 5432);
        AddTcpProbe(services, configuration, ResourceNames.WorkspaceBlobs, "Object storage", defaultPort: 8333);

        // Both llama-server instances expose an unauthenticated /health that only turns healthy once their
        // model is loaded, so a loading or crashed model shows up here, not just a dead process.
        var inference = configuration.GetSection(InferenceOptions.SectionName).Get<InferenceOptions>() ?? new InferenceOptions();
        AddHttpInfraProbe(services, "LLM", $"{ResourceNames.Llm}-probe", inference.ChatEndpoint);
        AddHttpInfraProbe(services, "Embeddings", $"{ResourceNames.Embeddings}-probe", inference.EmbeddingEndpoint);

        // Speech is optional, so a half that is not configured gets no probe (and never opens an incident).
        var speech = configuration.GetSection(SpeechOptions.SectionName).Get<SpeechOptions>() ?? new SpeechOptions();
        AddSpeechProbe(services, "Speech-to-text", ResourceNames.Whisper, speech.TranscriptionEndpoint);
        AddSpeechProbe(services, "Text-to-speech", ResourceNames.Tts, speech.SynthesisEndpoint);
    }

    /// <summary>
    /// The backup sidecar runs only under Compose. Its client is always registered so the Backups panel can say
    /// backups are not set up; the probe only exists when an endpoint is configured.
    /// </summary>
    private static void RegisterBackupMonitoring(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(BackupMonitorOptions.SectionName);
        services.AddOptions<BackupMonitorOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
        var backup = section.Get<BackupMonitorOptions>() ?? new BackupMonitorOptions();

        services.TryAddTransient<InternalApiKeyDelegatingHandler>();

#pragma warning disable EXTEXP0001
        services.AddHttpClient<IBackupClient, BackupClient>(client =>
        {
            if (backup.IsConfigured)
            {
                client.BaseAddress = backup.Endpoint;
            }

            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddHttpMessageHandler<InternalApiKeyDelegatingHandler>()
        .RemoveAllLoggers();
#pragma warning restore EXTEXP0001

        if (backup.IsConfigured)
        {
            services.AddSingleton<IHealthProbe, BackupHealthProbe>();
        }
    }

    private static void AddSpeechProbe(IServiceCollection services, string target, string resourceName, Uri? endpoint)
    {
        if (SpeechOptions.IsConfigured(endpoint))
        {
            AddHttpInfraProbe(services, target, $"{resourceName}-probe", endpoint!);
        }
    }

    private static void AddHttpInfraProbe(IServiceCollection services, string target, string clientName, Uri baseAddress)
    {
#pragma warning disable EXTEXP0001
        services.AddHttpClient(clientName, client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
        })
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
