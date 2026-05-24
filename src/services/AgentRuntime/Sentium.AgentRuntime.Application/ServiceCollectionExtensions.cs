using Sentium.AgentRuntime.Application.Agents;
using Sentium.AgentRuntime.Application.Agents.Native;
using Sentium.AgentRuntime.Application.Conversations;
using Sentium.AgentRuntime.Application.Orchestration;
using Sentium.AgentRuntime.Application.WorkflowManagement;
using Sentium.AgentRuntime.Application.WorkspaceManagement;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Orchestration;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.AgentRuntime.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeApplication(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, NatsEventBus>();
        services.AddHostedService<NatsMessageProcessor>();
        services.AddTransient<IAgentService, AgentService>();
        services.AddTransient<IConversationService, ConversationService>();
        services.AddTransient<IWorkflowService, WorkflowService>();
        services.AddTransient<IWorkspaceService, WorkspaceService>();
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
