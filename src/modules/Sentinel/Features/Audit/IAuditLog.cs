namespace Sentium.Sentinel.Features.Audit;

/// <summary>
/// Forensic audit log for PDP decisions. Implementations must be thread-safe.
/// </summary>
internal interface IAuditLog
{
    /// <summary>
    /// Stores the record. Never throws: a failed write is logged, because an audit failure must not break the
    /// caller's flow.
    /// </summary>
    ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of audit records (newest first) plus the total count.
    /// </summary>
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of audit records for a specific agent (newest first) plus the total count.
    /// </summary>
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetByAgentPagedAsync(string agentId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Aggregates the outcomes of the newest <paramref name="window"/> records.
    /// </summary>
    Task<AuditStatsDto> GetStatsAsync(int window, CancellationToken ct = default);

    /// <summary>
    /// Deletes the records older than <paramref name="cutoff"/>.
    /// </summary>
    /// <returns>The number of deleted records.</returns>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
