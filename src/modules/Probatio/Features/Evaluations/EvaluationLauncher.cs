using Microsoft.EntityFrameworkCore;
using Sentium.Core.Constants;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Common;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Validates and freezes everything an Evaluation needs, and builds the queued Evaluations. Callers save them and
/// put them on the <see cref="EvaluationQueue"/>.
/// </summary>
internal sealed class EvaluationLauncher(
    ProbatioDbContext db,
    IEvaluationAgentBuilder agents,
    ISettingsService settings,
    TimeProvider timeProvider)
{
    /// <param name="models">One entry per Evaluation; <see langword="null"/> stands for the user's default model.</param>
    public async Task<Result<EvaluationLaunch>> PrepareAsync(
        Guid userId,
        Guid suiteId,
        int? suiteVersion,
        string agentName,
        IReadOnlyList<string?> models,
        CancellationToken ct)
    {
        var suite = await db.EvalSuites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == suiteId, ct);
        if (suite is null)
        {
            return Result<EvaluationLaunch>.Invalid("The Eval Suite does not exist.");
        }

        var versionNumber = suiteVersion ?? suite.CurrentVersion;
        var version = await db.EvalSuiteVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.SuiteId == suite.Id && v.Version == versionNumber, ct);
        if (version is null)
        {
            return Result<EvaluationLaunch>.Invalid($"The Eval Suite has no version {versionNumber}.");
        }

        if (version.Cases.Count == 0)
        {
            return Result<EvaluationLaunch>.Invalid("This suite version has no cases to evaluate.");
        }

        var snapshots = new List<EvaluationAgentSnapshot>(models.Count);
        foreach (var model in models)
        {
            var snapshot = await agents.SnapshotAsync(agentName.Trim(), Blank(model), userId, ct);
            if (!snapshot.IsSuccess)
            {
                return Result<EvaluationLaunch>.Invalid(snapshot.Error ?? "The agent cannot be evaluated.");
            }

            snapshots.Add(snapshot.Value!);
        }

        var judge = await ResolveJudgeAsync(version.Cases, userId, ct);
        if (judge is { IsSuccess: false })
        {
            return Result<EvaluationLaunch>.Invalid($"The Judge cannot be used. {judge.Value.Error}");
        }

        return Result<EvaluationLaunch>.Success(new EvaluationLaunch(suite, version, judge?.Value, snapshots));
    }

    public IReadOnlyList<Evaluation> CreateEvaluations(EvaluationLaunch launch, Guid userId, int repetitions, Guid? benchmarkId)
    {
        ArgumentNullException.ThrowIfNull(launch);

        var now = timeProvider.GetUtcNow();
        return
        [
            .. launch.Agents.Select(agent => new Evaluation
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                BenchmarkId = benchmarkId,
                SuiteId = launch.Suite.Id,
                SuiteName = launch.Suite.Name,
                SuiteVersion = launch.Version.Version,
                AgentName = agent.AgentName,
                Model = agent.Model,
                JudgeModel = launch.JudgeModel,
                Repetitions = repetitions,
                Status = EvaluationStatus.Queued,
                CaseCount = launch.Version.Cases.Count,
                Cases = launch.Version.Cases,
                Agent = agent,
                CreatedAt = now,
            }),
        ];
    }

    /// <summary>
    /// The user's Judge (default: the default model), checked against the model catalog; <see langword="null"/>
    /// when no case has a quality Check, so an unusable Judge setting never blocks deterministic Evaluations.
    /// </summary>
    private async Task<Result<string>?> ResolveJudgeAsync(List<EvalCase> cases, Guid userId, CancellationToken ct)
    {
        if (!cases.Any(c => c.Checks.Any(check => EvalCheckRules.IsQuality(check.Type))))
        {
            return null;
        }

        var preferences = await settings.GetValueAsync<EvaluationSettings>(SettingsKeys.Evaluation, userId, ct);
        return await agents.ResolveModelAsync(preferences?.JudgeModel, userId, ct);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
