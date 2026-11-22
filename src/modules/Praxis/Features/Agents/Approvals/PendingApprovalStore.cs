using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Agents.Approvals;

/// <summary>
/// Pending approvals in the <c>PendingApprovals</c> table. An approval nobody decides within <see cref="Ttl"/> expires;
/// the retention sweep deletes it.
/// </summary>
internal sealed class PendingApprovalStore(PraxisDbContext db, TimeProvider timeProvider) : IPendingApprovalStore, IRetentionSweep
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task AddAsync(string requestId, PendingApproval approval, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestId);
        ArgumentNullException.ThrowIfNull(approval);

        db.PendingApprovals.Add(new PendingApprovalRecord
        {
            RequestId = requestId,
            UserId = approval.UserId,
            CreatedAt = timeProvider.GetUtcNow(),
            Payload = PendingApprovalSerializer.Serialize(approval)
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<PendingApproval?> TakeAsync(string requestId, Guid ownerId, CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow() - Ttl;
        var owned = db.PendingApprovals.Where(p => p.RequestId == requestId && p.UserId == ownerId && p.CreatedAt > cutoff);

        var payload = await owned.Select(p => p.Payload).FirstOrDefaultAsync(ct);
        if (payload is null)
        {
            return null;
        }

        // The delete is the claim: only the take that removes the row resumes the run.
        return await owned.ExecuteDeleteAsync(ct) == 1 ? PendingApprovalSerializer.Deserialize(payload) : null;
    }

    public Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - Ttl;
        return db.PendingApprovals.Where(p => p.CreatedAt <= cutoff).ExecuteDeleteAsync(ct);
    }
}
