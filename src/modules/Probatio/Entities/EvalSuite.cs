using Sentium.Core.Common;

namespace Sentium.Probatio.Entities;

/// <summary>
/// An Eval Suite, private to its owner. Its cases live in immutable <see cref="EvalSuiteVersion"/>s; the name and
/// description are labels and change without a new version.
/// </summary>
internal sealed class EvalSuite : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int CurrentVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Postgres <c>xmin</c>: two edits from the same base version cannot both create the next version.
    /// </summary>
    public uint RowVersion { get; set; }

    public List<EvalSuiteVersion> Versions { get; set; } = [];
}
