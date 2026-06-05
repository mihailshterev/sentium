using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;
using Sentium.Sentinel.Infrastructure.Audit;
using Sentium.Sentinel.Infrastructure.Data;
using Sentium.Sentinel.Infrastructure.Policies;
using Sentium.Sentinel.Infrastructure.Registry;
using Sentium.Shared.Constants;

namespace Sentium.Sentinel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        services.AddHttpContextAccessor();

        builder.AddInternalApiSecurity();

        builder.AddAuditedDbContext<SentinelDbContext>(ResourceNames.SentinelDb);

        services.AddScoped<IAuditLog, EfCoreAuditLog>();

        services.AddSingleton<IEventBus, NatsEventBus>();

        var ollamaUri = new Uri(builder.Configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");

        services.AddHttpClient(ResourceNames.Ollama, client =>
        {
            client.BaseAddress = ollamaUri;
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddLongRunningResilienceHandler(
            totalTimeout: TimeSpan.FromMinutes(5),
            attemptTimeout: TimeSpan.FromMinutes(2),
            retries: 0
        );

        var intentCheckModel = builder.Configuration["AI:ModelName"] ?? string.Empty;

        services.AddChatClient(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ResourceNames.Ollama);
            return new OllamaApiClient(httpClient) { SelectedModel = intentCheckModel };
        });

        services.AddSingleton<IPdpPolicy, SemanticIntentPolicy>();

        services.AddTransient<InternalApiKeyDelegatingHandler>();

        services.AddHttpClient(ServiceNames.Registry, client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Registry}");
        }).AddHttpMessageHandler<InternalApiKeyDelegatingHandler>();

        services.AddSingleton<IPdpRuntimeSettingsProvider, RegistryPdpSettingsProvider>();
        services.AddHostedService<SettingsSyncWorker>();

        return builder;
    }
}

