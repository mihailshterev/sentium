using AgentRuntime.Application.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRuntime.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeApplication(this IServiceCollection services)
    {
        services.AddHostedService<NatsAgentOrchestrator>();
        return services;
    }
}
