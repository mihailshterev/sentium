using Sentium.Core.Common;
using Sentium.Praxis.Contracts;

namespace Sentium.Probatio.Entities;

/// <summary>
/// One Eval Suite version x one Agent x one model x <see cref="Repetitions"/>, private to the user who started it.
/// The cases and the agent are frozen copies taken at start, so the Evaluation is unaffected when the suite, the
/// agent or the model later change or are deleted.
/// </summary>
internal sealed class Evaluation : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The Benchmark this Evaluation is part of, if any.
    /// </summary>
    public Guid? BenchmarkId { get; set; }

    /// <summary>
    /// The suite it was taken from; not a foreign key, the suite may since have been deleted.
    /// </summary>
    public Guid SuiteId { get; set; }

    public string SuiteName { get; set; } = null!;

    public int SuiteVersion { get; set; }

    public string AgentName { get; set; } = null!;

    public string Model { get; set; } = null!;

    /// <summary>
    /// The Judge frozen at start; <see langword="null"/> when the suite version has no quality Checks.
    /// </summary>
    public string? JudgeModel { get; set; }

    public int Repetitions { get; set; }

    public EvaluationStatus Status { get; set; }

    public string? Error { get; set; }

    public int CaseCount { get; set; }

    public int CompletedCaseCount { get; set; }

    public int PassedCaseCount { get; set; }

    public List<EvalCase> Cases { get; set; } = [];

    public EvaluationAgentSnapshot Agent { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
