using AgentRuntime.Core.Orchestration;
using AgentRuntime.Core.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Application.Orchestration;

public sealed class NatsMessageProcessor(
    INatsConnection nats,
    IServiceScopeFactory scopeFactory,
    ILogger<NatsMessageProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NATS Message Processor is starting and listening for events...");

        try
        {
            await foreach (var msg in nats.SubscribeAsync<string>(WorkflowEvents.AllEvents, cancellationToken: stoppingToken))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

                        var trigger = new WorkflowTrigger
                        {
                            TriggerType = msg.Subject,
                            Payload = msg.Data!
                        };

                        var result = await orchestrator.RunAsync(trigger, stoppingToken);
                        logger.LogInformation("Workflow completed for event {Subject} with explanation: {Explanation}", msg.Subject, result.Explanation);

                        foreach (var (Role, Text) in result.History)
                        {
                            logger.LogInformation(" - {Role}: {Text}", Role, Text);
                        }

                        await nats.PublishAsync(
                            $"insights.{msg.Subject}",
                            result,
                            serializer: NatsJsonSerializer<WorkflowResult>.Default,
                            cancellationToken: stoppingToken
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing NATS message on {Subject}", msg.Subject);
                    }
                }, stoppingToken);
            }
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
