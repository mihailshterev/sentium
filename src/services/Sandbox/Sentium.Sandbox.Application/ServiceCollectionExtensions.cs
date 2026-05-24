using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Sandbox.Application.Options;

namespace Sentium.Sandbox.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSandboxApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SandboxOptions>(configuration.GetSection(SandboxOptions.SectionName));
        services.AddScoped<SandboxOrchestrator>();

        return services;
    }
}
