namespace Sentium.Sentinel.Core.Audit;

/// <summary>
/// Forensic audit log for PDP decisions.
/// Implementations must be thread-safe.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Records a decision asynchronously. Never throws — failures are silently swallowed to avoid disrupting the caller.
    /// </summary>
    ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent audit records, newest first.
    /// </summary>
    IReadOnlyList<AuditRecord> GetRecent(int count = 100);

    /// <summary>
    /// Returns audit records for a specific agent, newest first.
    /// </summary>
    IReadOnlyList<AuditRecord> GetByAgent(string agentId, int count = 50);
}
