using IdentityProvider.Infrastructure.Data;
using IdentityProvider.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityProvider.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
    {
        services.AddDbContext<IdentityDbContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value.DefaultConnection));

        return services;
    }
}
