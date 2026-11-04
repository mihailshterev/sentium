using Sentium.Praxis.Extensions;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;
using Sentium.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Executes queued workflow runs (see <see cref="IWorkflowRunQueue"/>) with bounded concurrency, a per-attempt
/// timeout, and retries for genuine failures. Every run is persisted to the run history and emits exactly one
/// terminal frame (<see cref="AgentUpdateTypes.Done"/>, <see cref="AgentUpdateTypes.Error"/> or
/// <see cref="AgentUpdateTypes.Cancelled"/>) to its stream so the UI can never hang waiting for an outcome -
/// including runs interrupted, or never started, because the application shut down.
/// </summary>
public sealed class WorkflowRunProcessor(
    WorkflowRunQueue queue,
    IAgentStreamPublisher stream,
    IServiceScopeFactory scopeFactory,
    IWorkflowExecutionRegistry executionRegistry,
    IConfiguration configuration,
    ILogger<WorkflowRunProcessor> logger) : BackgroundService
{
    private const int DefaultMaxConcurrency = 4;
    private const int DefaultTimeoutMinutes = 120;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    private readonly int _maxConcurrency = ResolveMaxConcurrency(configuration);
    private readonly TimeSpan _workflowTimeout = ResolveWorkflowTimeout(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Workflow processor is listening (max concurrency {MaxConcurrency}, timeout {Timeout}).",
            _maxConcurrency, _workflowTimeout);

        try
        {
            await Parallel.ForEachAsync(
                queue.Reader.ReadAllAsync(stoppingToken),
                new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency, CancellationToken = stoppingToken },
                async (trigger, ct) => await ProcessAsync(trigger, ct));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Workflow processor is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Workflow processor encountered a fatal error.");
        }

        await RecordUnstartedRunsAsync();
    }

    private async Task ProcessAsync(WorkflowTrigger trigger, CancellationToken hostCt)
    {
        if (hostCt.IsCancellationRequested)
        {
            await RecordNotStartedAsync(trigger);
            return;
        }

        using var userCts = new CancellationTokenSource();
        executionRegistry.Register(trigger.StreamId, userCts);

        var startedAt = DateTime.UtcNow;

        try
        {
            EmitSystem(trigger.StreamId, "Workflow started", AgentUpdateTypes.Status);

            for (var attempt = 1; ; attempt++)
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt, userCts.Token);
                attemptCts.CancelAfter(_workflowTimeout);

                try
                {
                    if (attempt > 1)
                    {
                        await Task.Delay(RetryDelay, attemptCts.Token);
                    }

                    logger.LogInformation(
                        "Executing {TriggerType} workflow (stream {StreamId}, attempt {Attempt}/{Max}).",
                        trigger.TriggerType, trigger.StreamId, attempt, MaxAttempts);

                    await RunAndPersistAsync(trigger, startedAt, attemptCts.Token);
                    EmitSystem(trigger.StreamId, AgentUpdateTypes.Done, AgentUpdateTypes.Done);
                    return;
                }
                catch (OperationCanceledException) when (hostCt.IsCancellationRequested)
                {
                    logger.LogWarning("Shutdown interrupted the workflow for stream {StreamId}.", trigger.StreamId);
                    await SafePersistFailureAsync(trigger, startedAt, "[CANCELLED] Interrupted by application shutdown.");
                    EmitSystem(trigger.StreamId, "The application shut down before the workflow completed.", AgentUpdateTypes.Error);
                    return;
                }
                catch (OperationCanceledException) when (userCts.IsCancellationRequested)
                {
                    logger.LogInformation("Workflow for stream {StreamId} was stopped by the user.", trigger.StreamId);
                    await SafePersistFailureAsync(trigger, startedAt, "[CANCELLED] Stopped by user.");
                    EmitSystem(trigger.StreamId, "Run stopped by user.", AgentUpdateTypes.Cancelled);
                    return;
                }
                catch (OperationCanceledException) when (attemptCts.IsCancellationRequested)
                {
                    logger.LogError("Workflow for stream {StreamId} timed out after {Timeout}.", trigger.StreamId, _workflowTimeout);
                    await SafePersistFailureAsync(trigger, startedAt, $"[TIMEOUT] Workflow exceeded {_workflowTimeout.TotalMinutes:0} minute(s).");
                    EmitSystem(trigger.StreamId, "The workflow timed out before completing.", AgentUpdateTypes.Error);
                    return;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    logger.LogError(ex, "Attempt {Attempt}/{Max} of the workflow for stream {StreamId} failed; retrying.", attempt, MaxAttempts, trigger.StreamId);
                    EmitSystem(trigger.StreamId, $"Attempt {attempt} failed; retrying...", AgentUpdateTypes.Status);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Workflow for stream {StreamId} failed after {Max} attempts.", trigger.StreamId, MaxAttempts);
                    await SafePersistFailureAsync(trigger, startedAt, $"[FAILED] {ex.Message}");
                    EmitSystem(trigger.StreamId, "The workflow failed to complete.", AgentUpdateTypes.Error);
                    return;
                }
            }
        }
        finally
        {
            executionRegistry.Unregister(trigger.StreamId);
        }
    }

    private async Task RunAndPersistAsync(WorkflowTrigger trigger, DateTime startedAt, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

        var result = await orchestrator.RunAsync(trigger, ct);
        var completedAt = DateTime.UtcNow;

        try
        {
            var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
            await runRepo.AddAsync(new WorkflowRun
            {
                Id = Guid.NewGuid(),
                UserId = result.UserId,
                WorkflowId = result.WorkflowId,
                TriggerType = trigger.TriggerType,
                TriggerPayload = trigger.Payload,
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

        logger.LogInformation("Workflow complete for stream {StreamId}: {Explanation}", trigger.StreamId, result.Explanation);
    }

    /// <summary>
    /// Records runs still waiting in the queue at shutdown, so they appear in the history as not started
    /// and any client watching their stream gets a terminal frame.
    /// </summary>
    private async Task RecordUnstartedRunsAsync()
    {
        while (queue.Reader.TryRead(out var trigger))
        {
            await RecordNotStartedAsync(trigger);
        }
    }

    private async Task RecordNotStartedAsync(WorkflowTrigger trigger)
    {
        logger.LogWarning("Workflow for stream {StreamId} was still queued at shutdown and will not run.", trigger.StreamId);
        await SafePersistFailureAsync(trigger, DateTime.UtcNow, "[CANCELLED] Not started: the application shut down.");
        EmitSystem(trigger.StreamId, "The application shut down before the workflow started.", AgentUpdateTypes.Error);
    }

    private async Task SafePersistFailureAsync(WorkflowTrigger trigger, DateTime startedAt, string explanation)
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
                TriggerPayload = trigger.Payload,
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

    private void EmitSystem(string streamId, string text, string type)
    {
        try
        {
            stream.Emit(streamId, "System", text, type);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit '{Type}' frame for stream {StreamId}.", type, streamId);
        }
    }

    private static int ResolveMaxConcurrency(IConfiguration configuration)
        => int.TryParse(configuration["Orchestration:MaxConcurrentWorkflows"], out var configured) && configured > 0
            ? configured
            : DefaultMaxConcurrency;

    private static TimeSpan ResolveWorkflowTimeout(IConfiguration configuration)
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
}
