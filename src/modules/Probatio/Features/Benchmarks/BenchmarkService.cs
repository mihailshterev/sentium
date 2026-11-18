using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Evaluations;

namespace Sentium.Probatio.Features.Benchmarks;

internal sealed class BenchmarkService(
    ProbatioDbContext db,
    EvaluationLauncher launcher,
    IEvaluationService evaluations,
    IEvaluationAgentBuilder agents,
    EvaluationQueue queue,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IBenchmarkService
{
    public async Task<Result<BenchmarkSummaryDto>> StartAsync(StartBenchmarkRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result<BenchmarkSummaryDto>.Invalid("Benchmarks need a signed-in user.");
        }

        var launch = await launcher.PrepareAsync(userId, request.SuiteId, request.SuiteVersion, request.AgentName, [.. request.Models], ct);
        if (!launch.IsSuccess)
        {
            return Result<BenchmarkSummaryDto>.Invalid(launch.Error!);
        }

        var prepared = launch.Value!;
        var benchmark = new Benchmark
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SuiteId = prepared.Suite.Id,
            SuiteName = prepared.Suite.Name,
            SuiteVersion = prepared.Version.Version,
            AgentName = prepared.Agents[0].AgentName,
            Repetitions = request.Repetitions,
            JudgeModel = prepared.JudgeModel,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        var queued = launcher.CreateEvaluations(prepared, userId, request.Repetitions, benchmark.Id);

        db.Benchmarks.Add(benchmark);
        db.Evaluations.AddRange(queued);
        await db.SaveChangesAsync(ct);

        foreach (var evaluation in queued)
        {
            queue.Enqueue(evaluation.Id);
        }

        return Result<BenchmarkSummaryDto>.Success(ToSummary(benchmark, [.. queued.Select(EvaluationMapping.ToSummaryDto)]));
    }

    public async Task<PagedResponse<BenchmarkSummaryDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        var total = await db.Benchmarks.CountAsync(ct);
        var benchmarks = await db.Benchmarks
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = benchmarks.Select(b => b.Id).ToList();
        var members = await db.Evaluations
            .AsNoTracking()
            .Where(e => e.BenchmarkId != null && ids.Contains(e.BenchmarkId.Value))
            .OrderBy(e => e.Model)
            .Select(EvaluationMapping.ToSummary)
            .ToListAsync(ct);

        var items = benchmarks
            .Select(b => ToSummary(b, [.. members.Where(e => e.BenchmarkId == b.Id)]))
            .ToList();

        return PagedResponse<BenchmarkSummaryDto>.Create(items, total, page, pageSize);
    }

    public async Task<BenchmarkDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var benchmark = await db.Benchmarks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (benchmark is null)
        {
            return null;
        }

        var members = await db.Evaluations.AsNoTracking().Where(e => e.BenchmarkId == id).OrderBy(e => e.Model).ToListAsync(ct);
        var memberIds = members.Select(e => e.Id).ToList();
        var results = await db.EvaluationCaseResults.AsNoTracking().Where(r => memberIds.Contains(r.EvaluationId)).ToListAsync(ct);
        var servedModels = await agents.ListModelsAsync(ct);

        return new BenchmarkDto(
            benchmark.Id,
            benchmark.SuiteId,
            benchmark.SuiteName,
            benchmark.SuiteVersion,
            benchmark.AgentName,
            benchmark.Repetitions,
            benchmark.JudgeModel,
            benchmark.CreatedAt,
            [.. members.Select(e => EvaluationMapping.ToDto(e, results.Where(r => r.EvaluationId == e.Id), servedModels))]);
    }

    public async Task<Result<BenchmarkSummaryDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Benchmarks.AnyAsync(b => b.Id == id, ct))
        {
            return Result<BenchmarkSummaryDto>.NotFound();
        }

        var active = await db.Evaluations
            .Where(e => e.BenchmarkId == id && (e.Status == EvaluationStatus.Queued || e.Status == EvaluationStatus.Running))
            .Select(e => e.Id)
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            return Result<BenchmarkSummaryDto>.Conflict("The Benchmark has already finished.");
        }

        foreach (var evaluationId in active)
        {
            await evaluations.CancelAsync(evaluationId, ct);
        }

        return await LoadSummaryAsync(id, ct) is { } summary
            ? Result<BenchmarkSummaryDto>.Success(summary)
            : Result<BenchmarkSummaryDto>.NotFound();
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Benchmarks.AnyAsync(b => b.Id == id, ct))
        {
            return Result<bool>.NotFound();
        }

        var anyActive = await db.Evaluations.AnyAsync(
            e => e.BenchmarkId == id && (e.Status == EvaluationStatus.Queued || e.Status == EvaluationStatus.Running),
            ct);
        if (anyActive)
        {
            return Result<bool>.Conflict("Cancel the Benchmark before deleting it.");
        }

        await db.Benchmarks.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
        return Result<bool>.Success(true);
    }

    private async Task<BenchmarkSummaryDto?> LoadSummaryAsync(Guid id, CancellationToken ct)
    {
        var benchmark = await db.Benchmarks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (benchmark is null)
        {
            return null;
        }

        var members = await db.Evaluations.AsNoTracking().Where(e => e.BenchmarkId == id).Select(EvaluationMapping.ToSummary).ToListAsync(ct);
        return ToSummary(benchmark, members);
    }

    private static BenchmarkSummaryDto ToSummary(Benchmark benchmark, IReadOnlyList<EvaluationSummaryDto> members)
    {
        return new BenchmarkSummaryDto(
            benchmark.Id,
            benchmark.SuiteId,
            benchmark.SuiteName,
            benchmark.SuiteVersion,
            benchmark.AgentName,
            benchmark.Repetitions,
            benchmark.JudgeModel,
            benchmark.CreatedAt,
            [.. members.OrderBy(e => e.Model, StringComparer.Ordinal)]);
    }
}
