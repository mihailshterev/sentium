using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Agents.Approvals;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class PendingApprovalSerializerTests
{
    [Fact]
    public void RoundTrips_TheSession_TheApprovalRequest_AndTheRunContext()
    {
        var ownerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var call = new FunctionCallContent("call-1", "execute_code_sandbox", new Dictionary<string, object?> { ["code"] = "print(1)" });
        var approval = new PendingApproval(
            Session: JsonDocument.Parse("""{"stateBag":{"ConversationChatHistoryProvider":{"conversationId":"c1"}}}""").RootElement.Clone(),
            ApprovalRequest: new ToolApprovalRequestContent("req-1", call),
            ConversationId: conversationId,
            Model: "gemma-4-e4b",
            UserId: ownerId,
            CorrelationId: "corr-1",
            OriginalUserPrompt: "run it",
            AgentName: "TimelinePlanner");

        var restored = PendingApprovalSerializer.Deserialize(PendingApprovalSerializer.Serialize(approval));

        Assert.NotNull(restored);
        Assert.Equal("req-1", restored.ApprovalRequest.RequestId);
        var restoredCall = Assert.IsType<FunctionCallContent>(restored.ApprovalRequest.ToolCall);
        Assert.Equal("call-1", restoredCall.CallId);
        Assert.Equal("execute_code_sandbox", restoredCall.Name);
        Assert.Equal("print(1)", restoredCall.Arguments!["code"]?.ToString());
        Assert.Equal("c1", restored.Session.GetProperty("stateBag").GetProperty("ConversationChatHistoryProvider").GetProperty("conversationId").GetString());
        Assert.Equal((conversationId, "gemma-4-e4b", ownerId, "corr-1", "run it", "TimelinePlanner"),
            (restored.ConversationId!.Value, restored.Model, restored.UserId!.Value, restored.CorrelationId, restored.OriginalUserPrompt, restored.AgentName));
    }
}
