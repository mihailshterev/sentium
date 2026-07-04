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
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Sentium.AgentRuntime.Application.Orchestration;

/// <summary>
/// Consumes workflow trigger messages durably from JetStream and executes them. Each message is only
/// acknowledged after the run reaches a terminal state (success, recorded failure, or timeout) and the
/// result has been persisted, giving at-least-once delivery that survives a service restart. Every run
/// emits exactly one terminal frame (<see cref="AgentUpdateTypes.Done"/> or <see cref="AgentUpdateTypes.Error"/>)
/// to its stream so the UI can never hang waiting for an outcome.
/// </summary>
public sealed class NatsMessageProcessor(
    IEventBus bus,
    INatsConnection connection,
    IServiceScopeFactory scopeFactory,
    IWorkflowExecutionRegistry executionRegistry,
    IConfiguration configuration,
    ILogger<NatsMessageProcessor> logger) : BackgroundService
{
    private const int DefaultMaxConcurrency = 4;
    private const int DefaultTimeoutMinutes = 120;
    private const int MaxDeliver = 4;
    private static readonly TimeSpan NakDelay = TimeSpan.FromSeconds(3);

    private int _maxConcurrency = DefaultMaxConcurrency;
    private TimeSpan _workflowTimeout = TimeSpan.FromMinutes(DefaultTimeoutMinutes);
    private INatsJSConsumer? _consumer;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _maxConcurrency = ResolveMaxConcurrency();
        _workflowTimeout = ResolveWorkflowTimeout();

        var js = new NatsJSContext(connection);

        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(WorkflowEvents.StreamName, [WorkflowEvents.AllEvents])
            {
                Retention = StreamConfigRetention.Workqueue,
                Storage = StreamConfigStorage.File,
                MaxAge = TimeSpan.FromHours(1),
                DuplicateWindow = TimeSpan.FromMinutes(2)
            },
            cancellationToken);

        var ackWait = _workflowTimeout + TimeSpan.FromMinutes(2);

        _consumer = await js.CreateOrUpdateConsumerAsync(
            WorkflowEvents.StreamName,
            new ConsumerConfig(WorkflowEvents.ConsumerName)
            {
                DurableName = WorkflowEvents.ConsumerName,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = ackWait,
                MaxDeliver = MaxDeliver,
                MaxAckPending = _maxConcurrency
            },
            cancellationToken);

        logger.LogInformation(
            "Provisioned JetStream stream {Stream} and durable consumer {Consumer} (timeout {Timeout}, ackWait {AckWait}).",
            WorkflowEvents.StreamName, WorkflowEvents.ConsumerName, _workflowTimeout, ackWait);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_consumer is null)
        {
            logger.LogError("JetStream consumer was not provisioned; workflow processor cannot start.");
            return;
        }

        logger.LogInformation("Workflow processor is listening (max concurrency {MaxConcurrency}).", _maxConcurrency);

        await bus.SubscribeAsync<WorkflowCancelRequest>(WorkflowEvents.CancelSignal, OnCancelRequestedAsync, stoppingToken);

        try
        {
            var messages = _consumer.ConsumeAsync<byte[]>(cancellationToken: stoppingToken);

            await Parallel.ForEachAsync(
                messages,
                new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency, CancellationToken = stoppingToken },
                async (msg, ct) => await ProcessMessageAsync(msg, ct));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Workflow processor is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Workflow processor encountered a fatal error.");
        }
    }

    private async Task ProcessMessageAsync(INatsJSMsg<byte[]> msg, CancellationToken messageCt)
    {
        if (msg.Data is null || msg.Data.Length == 0)
        {
            logger.LogWarning("Received empty workflow message on {Subject}; discarding.", msg.Subject);
            await SettleAsync(msg, ack: true);
            return;
        }

        var payloadString = Encoding.UTF8.GetString(msg.Data);
        var trigger = new WorkflowTrigger
        {
            TriggerType = msg.Subject,
            Payload = payloadString,
            UserId = TryParseUserId(payloadString),
            StreamId = TryParseStreamId(payloadString) ?? msg.Subject
        };

        var deliveries = msg.Metadata?.NumDelivered ?? 1;
        var isFinalDelivery = deliveries >= MaxDeliver;

        using var userCts = new CancellationTokenSource();
        executionRegistry.Register(trigger.StreamId, userCts);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(messageCt, userCts.Token);
        timeoutCts.CancelAfter(_workflowTimeout);
        var ct = timeoutCts.Token;

        var startedAt = DateTime.UtcNow;

        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

            logger.LogInformation(
                "Executing workflow for {Subject} (stream {StreamId}, delivery {Delivery}/{Max}).",
                msg.Subject, trigger.StreamId, deliveries, MaxDeliver);

            await EmitSystemAsync(trigger.StreamId, "Workflow started", AgentUpdateTypes.Status);

            var result = await orchestrator.RunAsync(trigger, ct);
            var completedAt = DateTime.UtcNow;

            await PersistRunAsync(scope, trigger, payloadString, result, startedAt, completedAt);
            await EmitSystemAsync(trigger.StreamId, AgentUpdateTypes.Done, AgentUpdateTypes.Done);
            await SettleAsync(msg, ack: true);

            logger.LogInformation("Workflow complete for stream {StreamId}: {Explanation}", trigger.StreamId, result.Explanation);
        }
        catch (OperationCanceledException) when (messageCt.IsCancellationRequested)
        {
            logger.LogInformation("Shutdown during workflow for stream {StreamId}; it will be redelivered.", trigger.StreamId);
            throw;
        }
        catch (OperationCanceledException) when (userCts.IsCancellationRequested)
        {
            logger.LogInformation("Workflow for stream {StreamId} was stopped by the user.", trigger.StreamId);
            await SafePersistFailureAsync(trigger, payloadString, startedAt, "[CANCELLED] Stopped by user.");
            await EmitSystemAsync(trigger.StreamId, "Run stopped by user.", AgentUpdateTypes.Cancelled);
            await SettleAsync(msg, ack: true);
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Workflow for stream {StreamId} timed out after {Timeout}.", trigger.StreamId, _workflowTimeout);
            await SafePersistFailureAsync(trigger, payloadString, startedAt, $"[TIMEOUT] Workflow exceeded {_workflowTimeout.TotalMinutes:0} minute(s).");
            await EmitSystemAsync(trigger.StreamId, "The workflow timed out before completing.", AgentUpdateTypes.Error);
            await SettleAsync(msg, ack: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing workflow for stream {StreamId} on {Subject}.", trigger.StreamId, msg.Subject);

            if (isFinalDelivery)
            {
                await SafePersistFailureAsync(trigger, payloadString, startedAt, $"[FAILED] {ex.Message}");
                await EmitSystemAsync(trigger.StreamId, "The workflow failed to complete.", AgentUpdateTypes.Error);
                await SettleAsync(msg, ack: true);
            }
            else
            {
                await EmitSystemAsync(trigger.StreamId, $"Attempt {deliveries} failed; retrying...", AgentUpdateTypes.Status);
                await SettleAsync(msg, ack: false);
            }
        }
        finally
        {
            executionRegistry.Unregister(trigger.StreamId);
        }
    }

    private Task OnCancelRequestedAsync(NatsMsg<WorkflowCancelRequest> msg)
    {
        var streamId = msg.Data?.StreamId;
        if (string.IsNullOrWhiteSpace(streamId))
        {
            return Task.CompletedTask;
        }

        if (executionRegistry.Cancel(streamId))
        {
            logger.LogInformation("Cancellation requested for stream {StreamId}; signalling the run.", streamId);
        }
        else if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Cancellation for stream {StreamId} arrived but no run is tracked on this instance.", streamId);
        }

        return Task.CompletedTask;
    }

    private async Task PersistRunAsync(IServiceScope scope, WorkflowTrigger trigger, string payload, WorkflowResult result, DateTime startedAt, DateTime completedAt)
    {
        try
        {
            var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
            await runRepo.AddAsync(new WorkflowRun
            {
                Id = Guid.NewGuid(),
                UserId = result.UserId,
                WorkflowId = result.WorkflowId,
                TriggerType = trigger.TriggerType,
                TriggerPayload = payload,
                Explanation = result.Explanation ?? string.Empty,
                Risk = result.Risk?.ToString() ?? string.Empty,
                Recommendation = result.Recommendation?.ToString() ?? string.Empty,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Logs = [.. result.StreamLog]
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist workflow run result for stream {StreamId}.", trigger.StreamId);
        }
    }

    private async Task SafePersistFailureAsync(WorkflowTrigger trigger, string payload, DateTime startedAt, string explanation)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
            await runRepo.AddAsync(new WorkflowRun
            {
                Id = Guid.NewGuid(),
                UserId = trigger.UserId,
                WorkflowId = null,
                TriggerType = trigger.TriggerType,
                TriggerPayload = payload,
                Explanation = explanation,
                Risk = string.Empty,
                Recommendation = string.Empty,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Logs = []
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist failed workflow run for stream {StreamId}.", trigger.StreamId);
        }
    }

    private async Task EmitSystemAsync(string streamId, string text, string type)
    {
        try
        {
            await bus.StreamAgentUpdateAsync(streamId, "System", text, type, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish '{Type}' frame for stream {StreamId}.", type, streamId);
        }
    }

    private async Task SettleAsync(INatsJSMsg<byte[]> msg, bool ack)
    {
        try
        {
            if (ack)
            {
                await msg.AckAsync(cancellationToken: CancellationToken.None);
            }
            else
            {
                await msg.NakAsync(delay: NakDelay, cancellationToken: CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to settle JetStream message on {Subject} (ack={Ack}).", msg.Subject, ack);
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

    private TimeSpan ResolveWorkflowTimeout()
    {
        var raw = configuration["Orchestration:WorkflowTimeout"];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (TimeSpan.TryParse(raw, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
            {
                return asTimeSpan;
            }

            if (double.TryParse(raw, out var asMinutes) && asMinutes > 0)
            {
                return TimeSpan.FromMinutes(asMinutes);
            }
        }

        return TimeSpan.FromMinutes(DefaultTimeoutMinutes);
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

    private string? TryParseStreamId(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("streamId", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Could not parse streamId from workflow trigger payload; falling back to the trigger subject.");
        }

        return null;
    }
}
