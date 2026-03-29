using System.Text;
using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentRuntime.Application.Orchestration;

public sealed class NatsMessageProcessor(
    IEventBus bus,
    IServiceScopeFactory scopeFactory,
    ILogger<NatsMessageProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NATS Message Processor is starting and listening for events...");

        try
        {
            await bus.SubscribeAsync<byte[]>(WorkflowEvents.AllEvents, async (msg) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

                    var payloadString = Encoding.UTF8.GetString(msg.Data!);

                    logger.LogInformation("Triggering Workflow for Subject: {Subject}", msg.Subject);

                    var trigger = new WorkflowTrigger
                    {
                        TriggerType = msg.Subject,
                        Payload = payloadString
                    };

                    var result = await orchestrator.RunAsync(trigger, stoppingToken);

                    logger.LogInformation("Workflow Complete. Result: {Explanation}", result.Explanation);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing NATS message on {Subject}", msg.Subject);
                }
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("NATS Message Processor is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "NATS Message Processor encountered a fatal error.");
        }
    }
}
