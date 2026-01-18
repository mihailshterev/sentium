using Microsoft.Extensions.DependencyInjection;

namespace IdentityProvider.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
