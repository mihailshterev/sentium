using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Realtime;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Titles;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Executes queued workflow runs (see <see cref="IWorkflowRunQueue"/>) with bounded concurrency, a per-attempt
/// timeout, and retries for genuine failures. Every run is persisted to the run history and emits exactly one
/// terminal frame (<see cref="AgentUpdateTypes.Done"/>, <see cref="AgentUpdateTypes.Error"/> or
/// <see cref="AgentUpdateTypes.Cancelled"/>) to its stream so the UI can never hang waiting for an outcome -
/// including runs interrupted, or never started, because the application shut down. Every outcome except a stop by
/// the user also notifies the run's user (see <see cref="WorkflowRunNotifier"/>). A Schedule's run additionally
/// releases its <see cref="ScheduledRunGate"/> slot and reports its end to every <see cref="IUnattendedRunObserver"/>.
/// </summary>
internal sealed class WorkflowRunProcessor(
    WorkflowRunQueue queue,
    IAgentStreamPublisher stream,
    IServiceScopeFactory scopeFactory,
    IWorkflowExecutionRegistry executionRegistry,
    IPdpContextAccessor runContext,
    WorkflowRunNotifier runNotifier,
    ScheduledRunGate scheduledRunGate,
    IEnumerable<IUnattendedRunObserver> runObservers,
    IRealtimePublisher realtime,
    IOptions<StreamingOptions> options,
    ILogger<WorkflowRunProcessor> logger) : BackgroundService
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    private readonly int _maxConcurrency = options.Value.MaxConcurrentWorkflows;
    private readonly TimeSpan _workflowTimeout = options.Value.WorkflowTimeout;

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
        executionRegistry.Register(trigger.StreamId, trigger.UserId, userCts, DescribeForOverview(trigger));

        var startedAt = DateTime.UtcNow;
        var end = new RunEnd(UnattendedRunStatus.Interrupted, null);

        try
        {
            end = await ExecuteWithRetriesAsync(trigger, startedAt, userCts, hostCt);
        }
        finally
        {
            executionRegistry.Unregister(trigger.StreamId);

            if (trigger.Schedule is { } schedule)
            {
                scheduledRunGate.Release(schedule.ScheduleId);
                await ReportScheduledEndAsync(trigger, schedule, end);
            }
        }
    }

    private async Task<RunEnd> ExecuteWithRetriesAsync(WorkflowTrigger trigger, DateTime startedAt, CancellationTokenSource userCts, CancellationToken hostCt)
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

                var (run, answer) = await RunAndPersistAsync(trigger, startedAt, attemptCts.Token);
                EmitSystem(trigger.StreamId, AgentUpdateTypes.Done, AgentUpdateTypes.Done);
                await runNotifier.NotifyAsync(trigger, run, WorkflowRunOutcome.Completed, answer, CancellationToken.None);
                return new RunEnd(UnattendedRunStatus.Completed, run);
            }
            catch (OperationCanceledException) when (hostCt.IsCancellationRequested)
            {
                logger.LogWarning("Shutdown interrupted the workflow for stream {StreamId}.", trigger.StreamId);
                var run = await RecordFailureAsync(trigger, startedAt, "[CANCELLED] Interrupted by application shutdown.", WorkflowRunOutcome.Interrupted);
                EmitSystem(trigger.StreamId, "The application shut down before the workflow completed.", AgentUpdateTypes.Error);
                return new RunEnd(UnattendedRunStatus.Interrupted, run);
            }
            catch (OperationCanceledException) when (userCts.IsCancellationRequested)
            {
                logger.LogInformation("Workflow for stream {StreamId} was stopped by the user.", trigger.StreamId);
                var run = await SafePersistFailureAsync(trigger, startedAt, "[CANCELLED] Stopped by user.");
                EmitSystem(trigger.StreamId, "Run stopped by user.", AgentUpdateTypes.Cancelled);
                return new RunEnd(UnattendedRunStatus.Cancelled, run);
            }
            catch (OperationCanceledException) when (attemptCts.IsCancellationRequested)
            {
                logger.LogError("Workflow for stream {StreamId} timed out after {Timeout}.", trigger.StreamId, _workflowTimeout);
                var run = await RecordFailureAsync(trigger, startedAt, $"[TIMEOUT] Workflow exceeded {_workflowTimeout.TotalMinutes:0} minute(s).", WorkflowRunOutcome.TimedOut);
                EmitSystem(trigger.StreamId, "The workflow timed out before completing.", AgentUpdateTypes.Error);
                return new RunEnd(UnattendedRunStatus.TimedOut, run);
            }
            catch (UnrunnableWorkflowException ex)
            {
                logger.LogWarning("Workflow for stream {StreamId} cannot run: {Reason}", trigger.StreamId, ex.Message);
                var run = await RecordFailureAsync(trigger, startedAt, $"[FAILED] {ex.Message}", WorkflowRunOutcome.Failed);
                EmitSystem(trigger.StreamId, ex.Message, AgentUpdateTypes.Error);
                return new RunEnd(UnattendedRunStatus.Failed, run);
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogError(ex, "Attempt {Attempt}/{Max} of the workflow for stream {StreamId} failed; retrying.", attempt, MaxAttempts, trigger.StreamId);
                EmitSystem(trigger.StreamId, $"Attempt {attempt} failed; retrying...", AgentUpdateTypes.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow for stream {StreamId} failed after {Max} attempts.", trigger.StreamId, MaxAttempts);
                var run = await RecordFailureAsync(trigger, startedAt, $"[FAILED] {ex.Message}", WorkflowRunOutcome.Failed);
                EmitSystem(trigger.StreamId, "The workflow failed to complete.", AgentUpdateTypes.Error);
                return new RunEnd(UnattendedRunStatus.Failed, run);
            }
        }
    }

    private async Task ReportScheduledEndAsync(WorkflowTrigger trigger, ScheduledRun schedule, RunEnd end)
    {
        if (trigger.UserId is not { } ownerId)
        {
            return;
        }

        var completion = new UnattendedRunCompletion(schedule.ScheduleId, ownerId, end.Run?.Id, end.Status, DateTimeOffset.UtcNow);

        foreach (var observer in runObservers)
        {
            try
            {
                await observer.OnRunCompletedAsync(completion, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Run observer {Observer} failed for schedule {ScheduleId}.", observer.GetType().Name, schedule.ScheduleId);
            }
        }
    }

    private async Task<(PersistedRun? Run, string Answer)> RunAndPersistAsync(WorkflowTrigger trigger, DateTime startedAt, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        // A user's run sees exactly what that user's own request would (the query filters apply); only a run without a
        // user is system work.
        var identity = scope.ServiceProvider.GetRequiredService<SystemScopeContext>();
        if (trigger.UserId is { } userId)
        {
            identity.ActAs(userId);
        }
        else
        {
            identity.Activate();
        }

        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

        // The run context is AsyncLocal: it must be set here, in the method that awaits the run, so every
        // agent, tool and policy check inside the workflow sees the acting user. Values assigned inside a
        // callee's async method (e.g. the agent factory) never flow back out to this caller.
        runContext.UserId = trigger.UserId;
        runContext.CorrelationId = trigger.StreamId;
        runContext.OriginalUserPrompt = trigger.Activity;
        runContext.Unattended = trigger.Schedule?.Policy;

        var title = ResolveScenario(trigger) is { } scenario
            ? scope.ServiceProvider.GetRequiredService<ITitleGenerator>().GenerateAsync(scenario, ct)
            : Task.FromResult<string?>(null);

        var result = await orchestrator.RunAsync(trigger, ct);
        var completedAt = DateTime.UtcNow;
        PersistedRun? persisted = null;

        try
        {
            var run = new PersistedRun(Guid.NewGuid(), await title);
            var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
            await runRepo.AddAsync(new WorkflowRun
            {
                Id = run.Id,
                UserId = result.UserId,
                WorkflowId = result.WorkflowId,
                Title = run.Title,
                TriggerType = trigger.TriggerType,
                TriggerPayload = trigger.ToPayloadJson(),
                Explanation = result.Explanation ?? string.Empty,
                Risk = result.Risk?.ToString() ?? string.Empty,
                Recommendation = result.Recommendation?.ToString() ?? string.Empty,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Logs = [.. result.StreamLog]
            }, CancellationToken.None);
            persisted = run;
            PublishRunRecorded(trigger.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist workflow run result for stream {StreamId}.", trigger.StreamId);
        }

        logger.LogInformation("Workflow complete for stream {StreamId}: {Explanation}", trigger.StreamId, result.Explanation);
        return (persisted, result.Explanation ?? string.Empty);
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
        await RecordFailureAsync(trigger, DateTime.UtcNow, "[CANCELLED] Not started: the application shut down.", WorkflowRunOutcome.Interrupted);
        EmitSystem(trigger.StreamId, "The application shut down before the workflow started.", AgentUpdateTypes.Error);
    }

    private async Task<PersistedRun?> RecordFailureAsync(WorkflowTrigger trigger, DateTime startedAt, string explanation, WorkflowRunOutcome outcome)
    {
        var run = await SafePersistFailureAsync(trigger, startedAt, explanation);
        await runNotifier.NotifyAsync(trigger, run, outcome, explanation, CancellationToken.None);
        return run;
    }

    private async Task<PersistedRun?> SafePersistFailureAsync(WorkflowTrigger trigger, DateTime startedAt, string explanation)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var run = new PersistedRun(Guid.NewGuid(), TitleGenerator.FromText(ResolveScenario(trigger)));
            var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
            await runRepo.AddAsync(new WorkflowRun
            {
                Id = run.Id,
                UserId = trigger.UserId,
                WorkflowId = null,
                Title = run.Title,
                TriggerType = trigger.TriggerType,
                TriggerPayload = trigger.ToPayloadJson(),
                Explanation = explanation,
                Risk = string.Empty,
                Recommendation = string.Empty,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Logs = []
            }, CancellationToken.None);
            PublishRunRecorded(trigger.UserId);
            return run;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist failed workflow run for stream {StreamId}.", trigger.StreamId);
            return null;
        }
    }

    /// <summary>
    /// The scenario the run was started with, or <see langword="null"/> when it carries none.
    /// </summary>
    private static string? ResolveScenario(WorkflowTrigger trigger)
    {
        return string.IsNullOrWhiteSpace(trigger.Activity) ? null : trigger.Activity;
    }

    private static string DescribeForOverview(WorkflowTrigger trigger)
    {
        if (trigger.Schedule is { } schedule)
        {
            return $"Schedule: {schedule.ScheduleName}";
        }

        return trigger.WorkflowName is { Length: > 0 } workflowName ? $"Workflow: {workflowName}" : "Dynamic workflow";
    }

    /// <summary>
    /// Sovereigns see every user's run history, so they hear about every run.
    /// </summary>
    private void PublishRunRecorded(Guid? userId)
    {
        var audience = userId is { } owner ? RealtimeAudience.User(owner, includeSovereigns: true) : RealtimeAudience.Sovereigns;
        realtime.Publish(new RealtimeEvent(RealtimeTopics.WorkflowRuns, audience));
    }

    private void EmitSystem(string streamId, string text, string type)
    {
        try
        {
            stream.Publish(streamId, new AgentStreamUpdate(WorkflowTimeline.SystemAuthor, text, type));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit '{Type}' frame for stream {StreamId}.", type, streamId);
        }
    }

    private sealed record RunEnd(UnattendedRunStatus Status, PersistedRun? Run);
}
