using Sentium.Core.Common;

namespace Sentium.Vigil.Entities;

/// <summary>
/// An incident Vigil opened, kept so the history survives restarts. <see cref="ResolvedAt"/> is
/// <see langword="null"/> while it is open. Kind, severity and status are stored by name.
/// </summary>
internal sealed class IncidentRecord : INonAudited
{
    public Guid Id { get; set; }

    public string Target { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? Description { get; set; }

    public string LastObservedStatus { get; set; } = null!;
}
