namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// The open incident per target, held in memory for the monitoring loop, and every incident written to the database
/// so the history survives restarts.
/// </summary>
internal interface IIncidentStore
{
    int OpenCount { get; }

    /// <summary>
    /// Returns the open incident for a target, or null if none is open.
    /// </summary>
    Incident? GetOpen(string target);

    /// <summary>
    /// Opens a new incident for a target. Returns null if one is already open (deduplication).
    /// </summary>
    Task<Incident?> OpenAsync(string target, ComponentKind kind, IncidentSeverity severity, ServiceStatus observedStatus, string? description, CancellationToken ct = default);

    /// <summary>
    /// Resolves the open incident for a target. Returns the resolved incident, or null if none was open.
    /// </summary>
    Task<Incident?> ResolveAsync(string target, CancellationToken ct = default);

    /// <summary>
    /// Open incidents followed by up to <paramref name="limit"/> resolved ones, newest first. When the database cannot
    /// be read, the resolved ones are those seen since the process started.
    /// </summary>
    Task<IReadOnlyList<Incident>> GetRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Reloads the incidents still open when the process last stopped. Those on a target nobody probes any more are
    /// resolved instead, since nothing would ever resolve them.
    /// </summary>
    Task RestoreOpenAsync(IReadOnlySet<string> monitoredTargets, CancellationToken ct = default);
}
