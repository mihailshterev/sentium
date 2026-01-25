using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using AgentRuntime.Infrastructure.Ollama;
using AgentRuntime.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAgentRuntimeFactory, OllamaAgentRuntimeFactory>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        services.AddSingleton<IAgentTool, ThreatIntelTool>();
        services.AddSingleton<IAgentTool, FileReadTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        return services;
    }
}