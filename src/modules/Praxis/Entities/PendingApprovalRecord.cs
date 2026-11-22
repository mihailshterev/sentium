using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

/// <summary>
/// An agent run paused on a tool-approval request, kept until its user decides (or it expires), so the decision still
/// resumes the run after a restart.
/// </summary>
internal sealed class PendingApprovalRecord : INonAudited
{
    public string RequestId { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The serialized <c>PendingApproval</c>: the agent session and the approval request.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
