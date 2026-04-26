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
        services.AddTransient<IAgentTool, FileReadTool>();
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
}
