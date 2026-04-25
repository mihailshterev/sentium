using AgentRuntime.Application.Agents;
using AgentRuntime.Application.Agents.Native;
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

        services.RegisterNativeAgents();

        return services;
    }

    private static void RegisterNativeAgents(this IServiceCollection services)
    {
        RegisterAgent<GeneralAssistant>(services, AgentRole.GeneralAssistant);
        RegisterAgent<PlannerAgent>(services, AgentRole.Planner);
        RegisterAgent<SecurityAnalyst>(services, AgentRole.SecurityAnalyst);
        RegisterAgent<SummaryAgent>(services, AgentRole.Summarizer);
        RegisterAgent<ThreatIntelAgent>(services, AgentRole.ThreatIntel);
        RegisterAgent<ForensicsAgent>(services, AgentRole.Forensics);
        RegisterAgent<ValidationAgent>(services, AgentRole.Validator);
    }

    private static void RegisterAgent<T>(IServiceCollection services, string name) where T : class, IAgent
    {
        services.AddKeyedTransient<IAgent, T>(name);
        services.AddTransient<IAgent, T>();
    }
}
