using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Core.Agents;

public sealed record PendingApproval(
    AIAgent Agent,
    AgentSession Session,
    ToolApprovalRequestContent ApprovalRequest,
    Guid? ConversationId,
    string Model,
    List<ChatMessage> ChatHistory,
    string OriginalUserPrompt = "",
    string CorrelationId = "");

public interface IPendingApprovalStore
{
    void Add(string requestId, PendingApproval approval);
    bool TryTake(string requestId, out PendingApproval? approval);
}
