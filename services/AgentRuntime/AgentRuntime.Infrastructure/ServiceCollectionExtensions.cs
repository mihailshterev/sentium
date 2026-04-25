using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Tools;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Infrastructure.Agents;
using AgentRuntime.Infrastructure.Conversations;
using AgentRuntime.Infrastructure.Data;
using AgentRuntime.Infrastructure.Tools;
using AgentRuntime.Infrastructure.WorkflowManagement;
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
            var client = new OllamaApiClient(new Uri("http://localhost:11434"), modelName);
            return new ChatClientBuilder(client)
                .UseFunctionInvocation()
                .UseOpenTelemetry()
                .Build();
        });

        services.AddTransient<IAgentTool, ThreatIntelTool>();
        services.AddTransient<IAgentTool, FileReadTool>();

        RegisterAgent<GeneralAssistant>(services, AgentRole.GeneralAssistant);
        RegisterAgent<PlannerAgent>(services, AgentRole.Planner);
        RegisterAgent<SecurityAnalyst>(services, AgentRole.SecurityAnalyst);
        RegisterAgent<SummaryAgent>(services, AgentRole.Summarizer);
        RegisterAgent<ThreatIntelAgent>(services, AgentRole.ThreatIntel);
        RegisterAgent<ForensicsAgent>(services, AgentRole.Forensics);
        RegisterAgent<ValidationAgent>(services, AgentRole.Validator);

        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<IAgentToolProvider, AgentToolProvider>();
        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddScoped<IAgentFactory, CompositeAgentFactory>();
        services.AddScoped<IAgentManager, AgentManager>();
        services.AddScoped<IConversationManager, ConversationManager>();
        services.AddScoped<IWorkflowManager, WorkflowManager>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        return services;
    }

    private static void RegisterAgent<T>(IServiceCollection services, string name) where T : class, IAgent
    {
        services.AddKeyedTransient<IAgent, T>(name);
        services.AddTransient<IAgent, T>();
    }
}
