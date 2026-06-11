namespace Sentium.Sentinel.Core.Audit;

/// <summary>
/// Forensic audit log for PDP decisions.
/// Implementations must be thread-safe.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Records a decision asynchronously.
    /// </summary>
    ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent audit records, newest first.
    /// </summary>
    Task<IReadOnlyList<AuditRecord>> GetRecentAsync(int count = 100, CancellationToken ct = default);

    /// <summary>
    /// Returns audit records for a specific agent, newest first.
    /// </summary>
    Task<IReadOnlyList<AuditRecord>> GetByAgentAsync(string agentId, int count = 50, CancellationToken ct = default);
}
