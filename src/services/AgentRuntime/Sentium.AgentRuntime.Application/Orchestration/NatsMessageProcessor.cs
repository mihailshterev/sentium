using System.Text;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Orchestration;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Application.Orchestration;

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

                    var startedAt = DateTime.Now;
                    var result = await orchestrator.RunAsync(trigger, stoppingToken);
                    var completedAt = DateTime.Now;

                    logger.LogInformation("Workflow Complete. Result: {Explanation}", result.Explanation);

                    try
                    {
                        var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
                        await runRepo.AddAsync(new WorkflowRun
                        {
                            Id = Guid.NewGuid(),
                            TriggerType = trigger.TriggerType,
                            TriggerPayload = payloadString,
                            Explanation = result.Explanation ?? string.Empty,
                            Risk = result.Risk?.ToString() ?? string.Empty,
                            Recommendation = result.Recommendation?.ToString() ?? string.Empty,
                            StartedAt = startedAt,
                            CompletedAt = completedAt
                        }, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to persist workflow run result.");
                    }
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
