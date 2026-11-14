using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Agents.Approvals;

/// <summary>
/// Everything needed to resume an agent run that paused on a tool-approval request - as data, not live objects. The agent
/// is rebuilt in the approving request's own DI scope and the serialized <see cref="Session"/> is restored into it.
/// </summary>
/// <param name="Session">The agent session serialized with <c>AIAgent.SerializeSessionAsync</c> when the run paused.</param>
/// <param name="ApprovalRequest">The request surfaced to the user; its <c>CreateResponse</c> produces the approval message.</param>
/// <param name="ConversationId">The persisted conversation the run belongs to, if any.</param>
/// <param name="UserId">The user who owns the run; only they may resume it.</param>
public sealed record PendingApproval(
    JsonElement Session,
    ToolApprovalRequestContent ApprovalRequest,
    Guid? ConversationId,
    string Model,
    Guid? UserId,
    string CorrelationId = "",
    string OriginalUserPrompt = "",
    string AgentName = AgentRole.GeneralAssistant);
