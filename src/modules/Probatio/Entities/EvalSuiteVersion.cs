using Sentium.Core.Common;

namespace Sentium.Probatio.Entities;

/// <summary>
/// The cases of an Eval Suite as they were at one version. Never changed after it is written, so an Evaluation
/// can keep pointing at the version it ran.
/// </summary>
internal sealed class EvalSuiteVersion : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid SuiteId { get; set; }

    public EvalSuite Suite { get; set; } = null!;

    public int Version { get; set; }

    public int CaseCount { get; set; }

    public List<EvalCase> Cases { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
