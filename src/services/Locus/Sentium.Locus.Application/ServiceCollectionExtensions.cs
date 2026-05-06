using Sentium.Locus.Application.Assets;
using Sentium.Locus.Application.Locations;
using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Locations;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Locus.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocusApplication(this IServiceCollection services)
    {
        services.AddTransient<ILocationService, LocationService>();
        services.AddTransient<IAssetService, AssetService>();

        return services;
    }
}
