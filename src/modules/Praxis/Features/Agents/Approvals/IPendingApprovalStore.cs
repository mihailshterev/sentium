namespace Sentium.Praxis.Features.Agents.Approvals;

internal interface IPendingApprovalStore
{
    void Add(string requestId, PendingApproval approval);

    /// <summary>
    /// Removes and returns the pending approval for <paramref name="requestId"/>, but only when it belongs to
    /// <paramref name="ownerId"/>. A lookup by any other user neither returns nor consumes the entry.
    /// </summary>
    bool TryTake(string requestId, Guid? ownerId, out PendingApproval? approval);
}
