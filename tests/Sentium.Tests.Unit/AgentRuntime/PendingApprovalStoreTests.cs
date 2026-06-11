using FluentAssertions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Infrastructure.Agents;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class PendingApprovalStoreTests
{
    private readonly PendingApprovalStore _store = new();

    private static PendingApproval CreateApproval(
        string? partialResponse = null,
        string? partialThought = null,
        IReadOnlyList<string>? partialToolCalls = null) =>
        new(
            Agent: null!,
            Session: null!,
            ApprovalRequest: null!,
            ConversationId: Guid.NewGuid(),
            Model: "gemma3:1b",
            ChatHistory: [],
            OriginalUserPrompt: "prompt",
            CorrelationId: "corr-1",
            UserId: Guid.NewGuid(),
            AgentName: "GeneralAssistant",
            PartialResponse: partialResponse,
            PartialThought: partialThought,
            PartialToolCalls: partialToolCalls);

    [Fact]
    public void TryTake_ReturnsStoredApproval_WhenRequestIdExists()
    {
        // Arrange
        var approval = CreateApproval();
        _store.Add("req-1", approval);

        // Act
        var found = _store.TryTake("req-1", out var taken);

        // Assert
        found.Should().BeTrue();
        taken.Should().BeSameAs(approval);
    }

    [Fact]
    public void TryTake_RoundTripsPartialContent_WhenSeeded()
    {
        // Arrange
        var approval = CreateApproval(
            partialResponse: "partial answer",
            partialThought: "partial reasoning",
            partialToolCalls: ["Calling search...", "Calling fetch..."]);
        _store.Add("req-2", approval);

        // Act
        _store.TryTake("req-2", out var taken);

        // Assert
        taken.Should().NotBeNull();
        taken.PartialResponse.Should().Be("partial answer");
        taken.PartialThought.Should().Be("partial reasoning");
        taken.PartialToolCalls.Should().BeEquivalentTo("Calling search...", "Calling fetch...");
    }

    [Fact]
    public void TryTake_ReturnsFalse_WhenRequestIdUnknown()
    {
        // Act
        var found = _store.TryTake("missing", out var taken);

        // Assert
        found.Should().BeFalse();
        taken.Should().BeNull();
    }

    [Fact]
    public void TryTake_ReturnsFalse_WhenCalledTwiceForSameRequestId()
    {
        // Arrange
        _store.Add("req-3", CreateApproval());
        _store.TryTake("req-3", out _);

        // Act
        var foundAgain = _store.TryTake("req-3", out var taken);

        // Assert
        foundAgain.Should().BeFalse();
        taken.Should().BeNull();
    }
}
