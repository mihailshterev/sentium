using AgentRuntime.Application.Orchestration;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Tools;
using AgentRuntime.Infrastructure.Agents;
using AgentRuntime.Infrastructure.Data;
using AgentRuntime.Infrastructure.Ollama;
using AgentRuntime.Infrastructure.Tools;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        services.AddDbContext<AgentRuntimeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("agentruntimedb"))
        );

        var modelName = configuration["AI:ModelName"] ?? "gemma3:1b";

        services.AddChatClient(sp =>
        {
            var uri = new Uri("http://localhost:11434");
            var client = new OllamaApiClient(uri)
            {
                SelectedModel = modelName
            };

            return new ChatClientBuilder(client)
                .UseFunctionInvocation()
                .Build();
        });

        services.AddSingleton<IAgentTool, ThreatIntelTool>();
        services.AddSingleton<IAgentTool, FileReadTool>();

        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<IAgentToolProvider, AgentToolProvider>();
        services.AddTransient<IAgentFactory, OllamaAgentFactory>();

        services.AddTransient<IOrchestrator, AgentOrchestrator>();
        services.AddSingleton<IEventBus, NatsEventBus>();

        return services;
    }
}
