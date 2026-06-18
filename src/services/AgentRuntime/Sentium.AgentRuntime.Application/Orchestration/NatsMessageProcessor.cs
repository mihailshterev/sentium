using System.Text;
using System.Text.Json;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Orchestration;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace Sentium.AgentRuntime.Application.Orchestration;

public sealed class NatsMessageProcessor(
    IEventBus bus,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NatsMessageProcessor> logger) : BackgroundService
{
    private const int DefaultMaxConcurrency = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxConcurrency = ResolveMaxConcurrency();
        logger.LogInformation("NATS Message Processor is starting and listening for events (max concurrency {MaxConcurrency})...", maxConcurrency);

        try
        {
            var stream = bus.SubscribeStreamAsync<byte[]>(WorkflowEvents.AllEvents, ct: stoppingToken);

            await Parallel.ForEachAsync(
                stream,
                new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = stoppingToken },
                async (msg, ct) => await ProcessMessageAsync(msg, ct)
            );
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

    private async Task ProcessMessageAsync(NatsMsg<byte[]> msg, CancellationToken ct)
    {
        WorkflowTrigger? trigger = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

            var payloadString = Encoding.UTF8.GetString(msg.Data!);

            logger.LogInformation("Triggering Workflow for Subject: {Subject}", msg.Subject);

            trigger = new WorkflowTrigger
            {
                TriggerType = msg.Subject,
                Payload = payloadString,
                UserId = TryParseUserId(payloadString)
            };

            var startedAt = DateTime.UtcNow;
            var result = await orchestrator.RunAsync(trigger, ct);
            var completedAt = DateTime.UtcNow;

            logger.LogInformation("Workflow Complete. Result: {Explanation}", result.Explanation);

            try
            {
                var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
                await runRepo.AddAsync(new WorkflowRun
                {
                    Id = Guid.NewGuid(),
                    UserId = result.UserId,
                    WorkflowId = result.WorkflowId,
                    TriggerType = trigger.TriggerType,
                    TriggerPayload = payloadString,
                    Explanation = result.Explanation ?? string.Empty,
                    Risk = result.Risk?.ToString() ?? string.Empty,
                    Recommendation = result.Recommendation?.ToString() ?? string.Empty,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Logs = [.. result.StreamLog]
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist workflow run result.");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing NATS message on {Subject}", msg.Subject);
        }
        finally
        {
            if (trigger is not null)
            {
                try
                {
                    await bus.StreamAgentUpdateAsync(trigger.TriggerType, "System", AgentUpdateTypes.Done, AgentUpdateTypes.Done, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to publish done signal for {TriggerType}", trigger.TriggerType);
                }
            }
        }
    }

    private int ResolveMaxConcurrency()
    {
        if (int.TryParse(configuration["Orchestration:MaxConcurrentWorkflows"], out var configured) && configured > 0)
        {
            return configured;
        }

        return DefaultMaxConcurrency;
    }

    private Guid? TryParseUserId(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("userId", out var prop) && prop.TryGetGuid(out var id))
            {
                return id;
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Could not parse userId from workflow trigger payload; treating as system-scoped.");
        }

        return null;
    }
}
