using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sentium.Core.Results;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Suites;

internal sealed class EvalSuiteService(ProbatioDbContext db, TimeProvider timeProvider) : IEvalSuiteService
{
    private const string StaleEditMessage = "The suite was changed since you opened it. Reload it and edit again.";

    public async Task<PagedResponse<EvalSuiteSummaryDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        var query = db.EvalSuites.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new EvalSuiteSummaryDto(
                s.Id,
                s.Name,
                s.Description,
                s.CurrentVersion,
                s.Versions.Where(v => v.Version == s.CurrentVersion).Select(v => v.CaseCount).FirstOrDefault(),
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(ct);

        return PagedResponse<EvalSuiteSummaryDto>.Create(items, total, page, pageSize);
    }

    public async Task<EvalSuiteDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var suite = await db.EvalSuites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return suite is null ? null : await LoadVersionAsync(suite, suite.CurrentVersion, ct);
    }

    public async Task<EvalSuiteDto?> GetVersionAsync(Guid id, int version, CancellationToken ct = default)
    {
        var suite = await db.EvalSuites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return suite is null ? null : await LoadVersionAsync(suite, version, ct);
    }

    public async Task<IReadOnlyList<EvalSuiteVersionDto>?> GetVersionsAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.EvalSuites.AnyAsync(s => s.Id == id, ct))
        {
            return null;
        }

        return await db.EvalSuiteVersions
            .AsNoTracking()
            .Where(v => v.SuiteId == id)
            .OrderByDescending(v => v.Version)
            .Select(v => new EvalSuiteVersionDto(v.Version, v.CaseCount, v.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<EvalSuiteDto> CreateAsync(CreateEvalSuiteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var suite = new EvalSuite
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Description = Blank(request.Description),
            CurrentVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var version = NewVersion(suite, EvalCaseNormalizer.Normalize(request.Cases), now);

        db.EvalSuites.Add(suite);
        db.EvalSuiteVersions.Add(version);
        await db.SaveChangesAsync(ct);

        return ToDto(suite, version);
    }

    public async Task<Result<EvalSuiteDto>> UpdateAsync(Guid id, UpdateEvalSuiteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var suite = await db.EvalSuites.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (suite is null)
        {
            return Result<EvalSuiteDto>.NotFound();
        }

        if (request.BaseVersion != suite.CurrentVersion)
        {
            return Result<EvalSuiteDto>.Conflict(StaleEditMessage);
        }

        var now = timeProvider.GetUtcNow();
        var cases = EvalCaseNormalizer.Normalize(request.Cases);
        var current = await db.EvalSuiteVersions
            .AsNoTracking()
            .FirstAsync(v => v.SuiteId == id && v.Version == suite.CurrentVersion, ct);

        suite.Name = request.Name.Trim();
        suite.Description = Blank(request.Description);
        suite.UpdatedAt = now;

        var shown = current;
        if (ProbatioDbContext.SerializeCases(cases) != ProbatioDbContext.SerializeCases(current.Cases))
        {
            suite.CurrentVersion++;
            shown = NewVersion(suite, cases, now);
            db.EvalSuiteVersions.Add(shown);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<EvalSuiteDto>.Conflict(StaleEditMessage);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Result<EvalSuiteDto>.Conflict(StaleEditMessage);
        }

        return Result<EvalSuiteDto>.Success(ToDto(suite, shown));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.EvalSuites.Where(s => s.Id == id).ExecuteDeleteAsync(ct) > 0;

    private async Task<EvalSuiteDto?> LoadVersionAsync(EvalSuite suite, int version, CancellationToken ct)
    {
        var stored = await db.EvalSuiteVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.SuiteId == suite.Id && v.Version == version, ct);

        return stored is null ? null : ToDto(suite, stored);
    }

    private static EvalSuiteVersion NewVersion(EvalSuite suite, List<EvalCase> cases, DateTimeOffset now)
    {
        return new EvalSuiteVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = suite.UserId,
            SuiteId = suite.Id,
            Version = suite.CurrentVersion,
            CaseCount = cases.Count,
            Cases = cases,
            CreatedAt = now
        };
    }

    private static EvalSuiteDto ToDto(EvalSuite suite, EvalSuiteVersion version)
    {
        return new EvalSuiteDto(
            suite.Id,
            suite.Name,
            suite.Description,
            version.Version,
            suite.CurrentVersion,
            version.CreatedAt,
            version.Cases,
            suite.CreatedAt,
            suite.UpdatedAt);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
