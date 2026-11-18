using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

internal sealed class EvaluationService(
    ProbatioDbContext db,
    EvaluationLauncher launcher,
    IEvaluationAgentBuilder agents,
    EvaluationQueue queue,
    EvaluationCancellations cancellations,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IEvaluationService
{
    public async Task<Result<EvaluationSummaryDto>> StartAsync(StartEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result<EvaluationSummaryDto>.Invalid("Evaluations need a signed-in user.");
        }

        var launch = await launcher.PrepareAsync(userId, request.SuiteId, request.SuiteVersion, request.AgentName, [request.Model], ct);
        if (!launch.IsSuccess)
        {
            return Result<EvaluationSummaryDto>.Invalid(launch.Error!);
        }

        var evaluation = launcher.CreateEvaluations(launch.Value!, userId, request.Repetitions, benchmarkId: null).Single();

        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync(ct);
        queue.Enqueue(evaluation.Id);

        return Result<EvaluationSummaryDto>.Success(EvaluationMapping.ToSummaryDto(evaluation));
    }

    public async Task<PagedResponse<EvaluationSummaryDto>> GetPagedAsync(Guid? suiteId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        var query = db.Evaluations.AsNoTracking();
        if (suiteId is { } id)
        {
            query = query.Where(e => e.SuiteId == id);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(EvaluationMapping.ToSummary)
            .ToListAsync(ct);

        return PagedResponse<EvaluationSummaryDto>.Create(items, total, page, pageSize);
    }

    public async Task<EvaluationDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var evaluation = await db.Evaluations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evaluation is null)
        {
            return null;
        }

        var results = await db.EvaluationCaseResults.AsNoTracking().Where(r => r.EvaluationId == id).ToListAsync(ct);
        return EvaluationMapping.ToDto(evaluation, results, await agents.ListModelsAsync(ct));
    }

    public async Task<Result<EvaluationSummaryDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var cancelledWhileQueued = await db.Evaluations
            .Where(e => e.Id == id && e.Status == EvaluationStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EvaluationStatus.Cancelled)
                .SetProperty(e => e.FinishedAt, now), ct);

        var evaluation = await db.Evaluations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evaluation is null)
        {
            return Result<EvaluationSummaryDto>.NotFound();
        }

        if (cancelledWhileQueued == 0)
        {
            if (evaluation.Status != EvaluationStatus.Running)
            {
                return Result<EvaluationSummaryDto>.Conflict("The Evaluation has already finished.");
            }

            cancellations.Cancel(id);
        }

        return Result<EvaluationSummaryDto>.Success(EvaluationMapping.ToSummaryDto(evaluation));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var status = await db.Evaluations.Where(e => e.Id == id).Select(e => (EvaluationStatus?)e.Status).FirstOrDefaultAsync(ct);
        if (status is null)
        {
            return Result<bool>.NotFound();
        }

        if (status is EvaluationStatus.Queued or EvaluationStatus.Running)
        {
            return Result<bool>.Conflict("Cancel the Evaluation before deleting it.");
        }

        var deleted = await db.Evaluations
            .Where(e => e.Id == id && e.Status != EvaluationStatus.Queued && e.Status != EvaluationStatus.Running)
            .ExecuteDeleteAsync(ct);

        return deleted == 1 ? Result<bool>.Success(true) : Result<bool>.Conflict("Cancel the Evaluation before deleting it.");
    }

}
