using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Security;
using Sentium.Probatio.Common;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Works through queued Evaluations one at a time: the model server serves one household, so Evaluations never
/// compete with each other for it. Every claimed Evaluation ends Completed, Failed or Cancelled.
/// </summary>
internal sealed class EvaluationWorker(
    IServiceScopeFactory scopeFactory,
    EvaluationQueue queue,
    EvaluationCancellations cancellations,
    TimeProvider timeProvider,
    ILogger<EvaluationWorker> logger) : BackgroundService
{
    private const string InterruptedMessage = "Sentium stopped before the Evaluation finished.";

    /// <summary>
    /// Fails Evaluations a previous process left running and queues the ones it left queued.
    /// </summary>
    public async Task RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<ProbatioDbContext>();

        var now = timeProvider.GetUtcNow();
        await db.Evaluations
            .Where(e => e.Status == EvaluationStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EvaluationStatus.Failed)
                .SetProperty(e => e.Error, InterruptedMessage)
                .SetProperty(e => e.FinishedAt, now), ct);

        var queued = await db.Evaluations
            .Where(e => e.Status == EvaluationStatus.Queued)
            .OrderBy(e => e.CreatedAt)
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var id in queued)
        {
            queue.Enqueue(id);
        }
    }

    /// <summary>
    /// Claims and executes one Evaluation; does nothing when it is no longer queued (for example cancelled while
    /// waiting).
    /// </summary>
    public async Task ProcessAsync(Guid evaluationId, CancellationToken stoppingToken)
    {
        using var cancellation = cancellations.Register(evaluationId, stoppingToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var db = scope.ServiceProvider.GetRequiredService<ProbatioDbContext>();

            if (!await TryClaimAsync(db, evaluationId, stoppingToken))
            {
                return;
            }

            await ExecuteClaimedAsync(scope.ServiceProvider, db, evaluationId, cancellation.Token, stoppingToken);
        }
        finally
        {
            cancellations.Unregister(evaluationId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverAsync(stoppingToken);

            await foreach (var id in queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(id, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; Evaluations left running are failed by RecoverAsync on the next start.
        }
    }

    private async Task<bool> TryClaimAsync(ProbatioDbContext db, Guid evaluationId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var claimed = await db.Evaluations
            .Where(e => e.Id == evaluationId && e.Status == EvaluationStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EvaluationStatus.Running)
                .SetProperty(e => e.StartedAt, now), ct);

        return claimed == 1;
    }

    private async Task ExecuteClaimedAsync(
        IServiceProvider services,
        ProbatioDbContext db,
        Guid evaluationId,
        CancellationToken ct,
        CancellationToken stoppingToken)
    {
        try
        {
            await services.GetRequiredService<EvaluationExecutor>().ExecuteAsync(evaluationId, ct);
            await FinishAsync(db, evaluationId, EvaluationStatus.Completed, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            var stopping = stoppingToken.IsCancellationRequested;
            await FinishAsync(db, evaluationId, stopping ? EvaluationStatus.Failed : EvaluationStatus.Cancelled, stopping ? InterruptedMessage : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation {EvaluationId} failed.", evaluationId);
            await FinishAsync(db, evaluationId, EvaluationStatus.Failed, ErrorText.Fit($"The Evaluation failed: {ex.Message}"));
        }
    }

    private async Task FinishAsync(ProbatioDbContext db, Guid evaluationId, EvaluationStatus status, string? error)
    {
        var now = timeProvider.GetUtcNow();
        await db.Evaluations
            .Where(e => e.Id == evaluationId && e.Status == EvaluationStatus.Running)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Status, status)
                    .SetProperty(e => e.Error, error)
                    .SetProperty(e => e.FinishedAt, now),
                CancellationToken.None);
    }
}
