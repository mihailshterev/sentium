using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// Runs queued <see cref="AssistantTurn"/>s concurrently, detached from the requests that started them: a reload, a
/// closed tab or a dropped connection no longer stops an answer. Every turn ends its relay stream with exactly one
/// terminal frame - <see cref="AgentUpdateTypes.Done"/> (also after pausing for an approval),
/// <see cref="AgentUpdateTypes.Cancelled"/> when its owner stopped it, or <see cref="AgentUpdateTypes.Error"/> -
/// including turns cut short or never started because the application shut down.
/// </summary>
internal sealed class AssistantTurnProcessor(
    AssistantTurnQueue queue,
    AssistantTurnRegistry registry,
    IAgentStreamPublisher stream,
    IServiceScopeFactory scopeFactory,
    IPdpContextAccessor runContext,
    IConfiguration configuration,
    ILogger<AssistantTurnProcessor> logger) : BackgroundService
{
    private const int DefaultMaxConcurrency = 8;
    private const string FailedMessage = "An unexpected error occurred while generating the response.";
    private const string ShutdownMessage = "Sentium stopped before the response finished.";
    private const string TimeoutMessage = "The response took too long and was stopped.";
    private const string StoppedMessage = "Stopped by user.";

    private static readonly TimeSpan DefaultTurnTimeout = TimeSpan.FromMinutes(30);

    private readonly int _maxConcurrency = ResolveMaxConcurrency(configuration);
    private readonly TimeSpan _turnTimeout = configuration.GetTimeSpan("Assistant:TurnTimeout", DefaultTurnTimeout);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                queue.Reader.ReadAllAsync(stoppingToken),
                new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency, CancellationToken = stoppingToken },
                ProcessAsync);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Assistant turn processor is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Assistant turn processor encountered a fatal error.");
        }

        while (queue.Reader.TryRead(out var turn))
        {
            Emit(turn.TurnId, AgentUpdateTypes.Error, ShutdownMessage);
            registry.Release(turn.TurnId);
        }
    }

    private async ValueTask ProcessAsync(AssistantTurn turn, CancellationToken hostCt)
    {
        try
        {
            var (type, text) = await RunAsync(turn, hostCt);
            Emit(turn.TurnId, type, text);
        }
        finally
        {
            registry.Release(turn.TurnId);
        }
    }

    private async Task<(string Type, string Text)> RunAsync(AssistantTurn turn, CancellationToken hostCt)
    {
        var stopToken = registry.StopToken(turn.TurnId);
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt, stopToken);
        turnCts.CancelAfter(_turnTimeout);

        try
        {
            // A turn stopped while it was queued never starts.
            turnCts.Token.ThrowIfCancellationRequested();

            await using var scope = scopeFactory.CreateAsyncScope();

            // The turn sees exactly what its user's own request would: the query filters apply, with no bypass.
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(turn.UserId);

            // The run context is AsyncLocal: set here, in the method that awaits the run, so it flows to every agent,
            // tool and policy check of the turn.
            runContext.UserId = turn.UserId;
            runContext.CorrelationId = turn.CorrelationId;
            runContext.OriginalUserPrompt = turn.OriginalUserPrompt;
            runContext.Unattended = null;

            var runner = scope.ServiceProvider.GetRequiredService<AssistantTurnRunner>();
            await runner.RunAsync(turn, new RelayUpdateSink(stream, turn.TurnId), turnCts.Token);

            return (AgentUpdateTypes.Done, AgentUpdateTypes.Done);
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            return (AgentUpdateTypes.Cancelled, StoppedMessage);
        }
        catch (OperationCanceledException) when (hostCt.IsCancellationRequested)
        {
            logger.LogWarning("Shutdown interrupted assistant turn {TurnId}.", turn.TurnId);
            return (AgentUpdateTypes.Error, ShutdownMessage);
        }
        catch (OperationCanceledException) when (turnCts.IsCancellationRequested)
        {
            logger.LogError("Assistant turn {TurnId} timed out after {Timeout}.", turn.TurnId, _turnTimeout);
            return (AgentUpdateTypes.Error, TimeoutMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while generating assistant turn {TurnId}. CorrelationId: {CorrelationId}", turn.TurnId, turn.CorrelationId);
            return (AgentUpdateTypes.Error, FailedMessage);
        }
    }

    private void Emit(string turnId, string type, string text)
    {
        try
        {
            stream.Publish(turnId, new AgentStreamUpdate(RelayUpdateSink.AssistantAuthor, text, type));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit the '{Type}' frame of assistant turn {TurnId}.", type, turnId);
        }
    }

    private static int ResolveMaxConcurrency(IConfiguration configuration)
    {
        return int.TryParse(configuration["Assistant:MaxConcurrentTurns"], out var configured) && configured > 0
            ? configured
            : DefaultMaxConcurrency;
    }
}
