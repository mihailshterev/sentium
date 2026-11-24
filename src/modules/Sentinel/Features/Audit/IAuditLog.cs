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
    /// Newest first.
    /// </summary>
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Newest first.
    /// </summary>
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetByAgentPagedAsync(string agentId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Aggregates the outcomes of the newest <paramref name="window"/> records.
    /// </summary>
    Task<AuditStatsDto> GetStatsAsync(int window, CancellationToken ct = default);

    /// <returns>The number of deleted records.</returns>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
