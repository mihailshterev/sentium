using AgentRuntime.Application.Orchestration;
using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Tools;
using AgentRuntime.Core.Workflows;
using AgentRuntime.Infrastructure.Agents;
using AgentRuntime.Infrastructure.Ollama;
using AgentRuntime.Infrastructure.Tools;
using Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Hosting;

namespace AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNats(poolSize: 1, options =>
        {
            return options with { Url = "nats://localhost:4222" };
        });
        services.AddSingleton<IAgentRegistry, AgentRegistry>();

        services.AddTransient<IAgentFactory, OllamaAgentFactory>(sp =>
        {
            return new OllamaAgentFactory(
                sp.GetRequiredService<IAgentRegistry>(),
                model: "gemma3:1b"
            );
        });

        services.AddSingleton<IAgentTool, ThreatIntelTool>();
        services.AddSingleton<IAgentTool, FileReadTool>();

        services.AddScoped<IAgentWorkflow, AgentWorkflow>();
        services.AddTransient<IOrchestrator, AgentOrchestrator>();


        services.AddSingleton<IEventBus, NatsEventBus>();

        return services;
    }
}
