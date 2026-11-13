using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Agents;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class PendingApprovalStoreTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private readonly PendingApprovalStore _store = new();

    private static PendingApproval CreateApproval(string requestId = "req") =>
        new(
            Session: JsonDocument.Parse("""{"stateBag":{"ConversationChatHistoryProvider":{"conversationId":"c1"}}}""").RootElement.Clone(),
            ApprovalRequest: new ToolApprovalRequestContent(requestId, new FunctionCallContent("call-1", "execute_code_sandbox")),
            ConversationId: Guid.NewGuid(),
            Model: "gemma3:1b",
            UserId: OwnerId,
            CorrelationId: "corr-1",
            OriginalUserPrompt: "prompt");

    [Fact]
    public void TryTake_ReturnsStoredApproval_WhenRequestIdExists()
    {
        // Arrange
        var approval = CreateApproval();
        _store.Add("req-1", approval);

        // Act
        var found = _store.TryTake("req-1", OwnerId, out var taken);

        // Assert
        found.Should().BeTrue();
        taken.Should().BeSameAs(approval);
    }

    [Fact]
    public void TryTake_RoundTripsTheSerializedSessionAndRequest()
    {
        // Arrange
        _store.Add("req-2", CreateApproval("req-2"));

        // Act
        _store.TryTake("req-2", OwnerId, out var taken);

        // Assert
        taken.Should().NotBeNull();
        taken.Session.GetProperty("stateBag").GetProperty("ConversationChatHistoryProvider").GetProperty("conversationId").GetString().Should().Be("c1");
        taken.ApprovalRequest.RequestId.Should().Be("req-2");
        taken.ApprovalRequest.ToolCall.Should().BeOfType<FunctionCallContent>().Which.Name.Should().Be("execute_code_sandbox");
    }

    [Fact]
    public void TryTake_ReturnsFalse_WhenRequestIdUnknown()
    {
        // Act
        var found = _store.TryTake("missing", OwnerId, out var taken);

        // Assert
        found.Should().BeFalse();
        taken.Should().BeNull();
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
        foundAgain.Should().BeFalse();
        taken.Should().BeNull();
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
        foundByStranger.Should().BeFalse();
        strangerTaken.Should().BeNull();
        foundByOwner.Should().BeTrue();
        ownerTaken.Should().BeSameAs(approval);
    }
}
