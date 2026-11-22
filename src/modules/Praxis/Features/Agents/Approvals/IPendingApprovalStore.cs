namespace Sentium.Praxis.Features.Agents.Approvals;

/// <summary>
/// Runs paused on a tool-approval request, kept in the database so a decision still resumes its run after a restart.
/// </summary>
internal interface IPendingApprovalStore
{
    Task AddAsync(string requestId, PendingApproval approval, CancellationToken ct = default);

    /// <summary>
    /// Removes and returns the pending approval for <paramref name="requestId"/>, but only when it belongs to
    /// <paramref name="ownerId"/> and has not expired. A lookup by any other user neither returns nor consumes the
    /// entry; of two concurrent takes, one wins.
    /// </summary>
    Task<PendingApproval?> TakeAsync(string requestId, Guid ownerId, CancellationToken ct = default);
}
