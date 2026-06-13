namespace Sentium.Watchdog.Core.Monitoring;

/// <summary>
/// In-memory store of incidents: the currently-open incident per target plus a bounded ring buffer
/// of recently resolved ones.
/// </summary>
public interface IIncidentStore
{
    /// <summary>
    /// Returns the open incident for a target, or null if none is open.
    /// </summary>
    Incident? GetOpen(string target);

    /// <summary>
    /// Opens a new incident for a target. Returns null if one is already open (deduplication).
    /// </summary>
    Incident? Open(string target, ComponentKind kind, IncidentSeverity severity, ServiceStatus observedStatus, string? description);

    /// <summary>
    /// Resolves the open incident for a target. Returns the resolved incident, or null if none was open.
    /// </summary>
    Incident? Resolve(string target);

    /// <summary>
    /// Returns all open incidents followed by recently resolved ones, newest first.
    /// </summary>
    IReadOnlyList<Incident> GetAll();

    /// <summary>
    /// Count of currently open incidents.
    /// </summary>
    int OpenCount { get; }
}
