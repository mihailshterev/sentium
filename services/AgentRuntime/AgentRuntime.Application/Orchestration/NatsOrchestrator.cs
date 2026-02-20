using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Application.Orchestration;

public sealed class NatsAgentOrchestrator : BackgroundService
{
    private readonly INatsConnection Nats;
    private readonly IOrchestrator Orchestrator;
    private readonly ILogger<NatsAgentOrchestrator> Logger;

    public NatsAgentOrchestrator(
        INatsConnection nats,
        IOrchestrator orchestrator,
        ILogger<NatsAgentOrchestrator> logger)
    {
        Nats = nats;
        Orchestrator = orchestrator;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("NATS Orchestrator is starting and listening for events...");

        try
        {
            await foreach (var msg in Nats.SubscribeAsync<string>(AgentEvents.AllEvents, cancellationToken: stoppingToken))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var trigger = new WorkflowTrigger
                        {
                            TriggerType = msg.Subject,
                            Payload = msg.Data!
                        };

                        var result = await Orchestrator.RunAsync(trigger, stoppingToken);
                        Logger.LogInformation("Workflow completed for event {Subject} with explanation: {Explanation}", msg.Subject, result.Explanation);
                        foreach (var (Role, Text) in result.History)
                        {
                            Logger.LogInformation(" - {Role}: {Text}", Role, Text);
                        }
                        // await Nats.PublishAsync(
                        //     $"insights.{msg.Subject}",
                        //     result,
                        //     serializer: NatsJsonSerializer<WorkflowResult>.Default,
                        //     cancellationToken: stoppingToken
                        // );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing NATS message on {Subject}", msg.Subject);
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("NATS Orchestrator is shutting down.");
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "NATS Orchestrator encountered a fatal error.");
        }
    }
}
