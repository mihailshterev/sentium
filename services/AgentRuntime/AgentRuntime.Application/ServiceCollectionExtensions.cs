using AgentRuntime.Application.Agents;
using AgentRuntime.Application.Orchestration;
using AgentRuntime.Core.Agents;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRuntime.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeApplication(this IServiceCollection services)
    {
        services.AddHostedService<NatsMessageProcessor>();
        services.AddTransient<IAgentService, AgentService>();
        return services;
    }
}
