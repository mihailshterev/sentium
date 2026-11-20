using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Agents.Approvals;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class PendingApprovalStoreTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private readonly PendingApprovalStore _store = new();

    private static PendingApproval CreateApproval(string requestId = "req")
    {
        return new(
            Session: JsonDocument.Parse("""{"stateBag":{"ConversationChatHistoryProvider":{"conversationId":"c1"}}}""").RootElement.Clone(),
            ApprovalRequest: new ToolApprovalRequestContent(requestId, new FunctionCallContent("call-1", "execute_code_sandbox")),
            ConversationId: Guid.NewGuid(),
            Model: "gemma3:1b",
            UserId: OwnerId,
            CorrelationId: "corr-1",
            OriginalUserPrompt: "prompt");
    }

    [Fact]
    public void TryTake_ReturnsStoredApproval_WhenRequestIdExists()
    {
        // Arrange
        var approval = CreateApproval();
        _store.Add("req-1", approval);

        // Act
        var found = _store.TryTake("req-1", OwnerId, out var taken);

        // Assert
        Assert.True(found);
        Assert.Same(approval, taken);
    }

    [Fact]
    public void TryTake_RoundTripsTheSerializedSessionAndRequest()
    {
        // Arrange
        _store.Add("req-2", CreateApproval("req-2"));

        // Act
        _store.TryTake("req-2", OwnerId, out var taken);

        // Assert
        Assert.NotNull(taken);
        Assert.Equal("c1", taken.Session.GetProperty("stateBag").GetProperty("ConversationChatHistoryProvider").GetProperty("conversationId").GetString());
        Assert.Equal("req-2", taken.ApprovalRequest.RequestId);
        Assert.Equal("execute_code_sandbox", Assert.IsType<FunctionCallContent>(taken.ApprovalRequest.ToolCall).Name);
    }

    [Fact]
    public void TryTake_ReturnsFalse_WhenRequestIdUnknown()
    {
        // Act
        var found = _store.TryTake("missing", OwnerId, out var taken);

        // Assert
        Assert.False(found);
        Assert.Null(taken);
    }

    [Fact]
    public void TryTake_ReturnsFalse_WhenCalledTwiceForSameRequestId()
    {
        // Arrange
        _store.Add("req-3", CreateApproval());
        _store.TryTake("req-3", OwnerId, out _);

        // Act
        var foundAgain = _store.TryTake("req-3", OwnerId, out var taken);

        // Assert
        Assert.False(foundAgain);
        Assert.Null(taken);
    }

    [Fact]
    public void TryTake_ReturnsFalse_AndKeepsEntry_WhenCallerIsNotTheOwner()
    {
        // Arrange
        var approval = CreateApproval();
        _store.Add("req-4", approval);

        // Act
        var foundByStranger = _store.TryTake("req-4", Guid.NewGuid(), out var strangerTaken);
        var foundByOwner = _store.TryTake("req-4", OwnerId, out var ownerTaken);

        // Assert
        Assert.False(foundByStranger);
        Assert.Null(strangerTaken);
        Assert.True(foundByOwner);
        Assert.Same(approval, ownerTaken);
    }
}
