using AgentRuntime.Application.Agents;
using AgentRuntime.Application.Conversations;
using AgentRuntime.Application.Orchestration;
using AgentRuntime.Application.WorkflowManagement;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.WorkflowManagement;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRuntime.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeApplication(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, NatsEventBus>();
        services.AddHostedService<NatsMessageProcessor>();
        services.AddTransient<IAgentService, AgentService>();
        services.AddTransient<IConversationService, ConversationService>();
        services.AddTransient<IWorkflowService, WorkflowService>();
        services.AddTransient<IOrchestrator, WorkflowOrchestrator>();

        return services;
    }
}
