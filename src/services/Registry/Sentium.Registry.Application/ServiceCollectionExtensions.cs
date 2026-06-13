using Microsoft.Extensions.DependencyInjection;
using Sentium.Registry.Application.Settings;
using Sentium.Registry.Core.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Registry.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRegistryApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<HarnessSettings>(
            SettingsKeys.Harness, SettingsScope.PerUser, c => c.Harness, (c, v) => c.Harness = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<PdpSettings>(
            SettingsKeys.Pdp, SettingsScope.Global, c => c.Pdp, (c, v) => c.Pdp = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<OllamaSettings>(
            SettingsKeys.Ollama, SettingsScope.Global, c => c.Ollama, (c, v) => c.Ollama = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<WatchdogSettings>(
            SettingsKeys.Watchdog, SettingsScope.Global, c => c.Watchdog, (c, v) => c.Watchdog = v)
        );

        services.AddSingleton<ISettingsCatalog, SettingsCatalog>();
        services.AddScoped<ISettingsService, SettingsService>();
        return services;
    }
}
