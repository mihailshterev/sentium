using Sentium.Core.Common;

namespace Sentium.Probatio.Entities;

/// <summary>
/// The outcome of one Eval Case in an Evaluation. The case passes only when every repetition passed.
/// </summary>
internal sealed class EvaluationCaseResult : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid EvaluationId { get; set; }

    public Guid CaseId { get; set; }

    public int Position { get; set; }

    public bool Passed { get; set; }

    public int PassedRepetitions { get; set; }

    public List<EvaluationRepetition> Repetitions { get; set; } = [];
}
