using NATS.Client.Core;
using AgentRuntime.Core.Workflows;
using AgentRuntime.Core.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using AgentRuntime.Core.Orchestration;

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
                        await Nats.PublishAsync($"insights.{msg.Subject}", result, cancellationToken: stoppingToken);
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
