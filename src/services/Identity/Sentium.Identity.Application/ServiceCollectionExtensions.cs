using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Identity.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
