using Sentium.Praxis.Features.Agents.Approvals;

namespace Sentium.Praxis.Features.Assistant;

internal sealed record ApprovalDecision(PendingApproval Pending, bool Approved);
