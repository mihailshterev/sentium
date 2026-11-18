using Sentium.Core.Common;

namespace Sentium.Probatio.Entities;

/// <summary>
/// A batch of Evaluations of one Eval Suite version and one Agent, one per model, graded by the same Judge so they
/// can be compared side by side. Private to the user who started it.
/// </summary>
internal sealed class Benchmark : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The suite it was taken from; not a foreign key, the suite may since have been deleted.
    /// </summary>
    public Guid SuiteId { get; set; }

    public string SuiteName { get; set; } = null!;

    public int SuiteVersion { get; set; }

    public string AgentName { get; set; } = null!;

    public int Repetitions { get; set; }

    /// <summary>
    /// The Judge shared by every Evaluation; <see langword="null"/> when the suite version has no quality Checks.
    /// </summary>
    public string? JudgeModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
