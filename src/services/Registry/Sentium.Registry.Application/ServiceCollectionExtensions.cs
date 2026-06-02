using Microsoft.Extensions.DependencyInjection;
using Sentium.Registry.Application.Settings;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRegistryApplication(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>();
        return services;
    }
}
