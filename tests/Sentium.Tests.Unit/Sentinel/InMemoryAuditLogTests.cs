using FluentAssertions;
using Sentium.Sentinel.Application.Audit;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class InMemoryAuditLogTests
{
    private readonly InMemoryAuditLog _sut = new();

    private static AuditRecord MakeRecord(string agentId = "agent-1", bool allowed = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = agentId,
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = "doc-1",
            Action = "read",
            UserPromptHash = "abc123",
            CorrelationId = Guid.NewGuid().ToString(),
            EvaluationDurationMs = 5,
            Allowed = allowed,
            Effect = allowed ? PolicyEffect.Allow : PolicyEffect.Deny,
            Reason = "test reason",
            Risk = PolicyRiskLevel.Low,
            TriggeredPolicies = []
        };

    [Fact]
    public async Task RecordAsync_AddsRecord_GetRecentReturnsMostRecentFirst()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var first = MakeRecord("agent-1");
        var second = MakeRecord("agent-2");

        // Act
        await _sut.RecordAsync(first, ct);
        await _sut.RecordAsync(second, ct);
        var result = await _sut.GetRecentAsync(10, ct);

        // Assert
        result.Should().HaveCount(2);
        result[0].AgentId.Should().Be("agent-2");
        result[1].AgentId.Should().Be("agent-1");
    }

    [Fact]
    public async Task GetRecentAsync_ClampsToMaxCapacity()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _sut.RecordAsync(MakeRecord(), ct);

        // Act - request more than what exists
        var result = await _sut.GetRecentAsync(999, ct);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByAgentAsync_FiltersToAgentId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _sut.RecordAsync(MakeRecord("agent-A"), ct);
        await _sut.RecordAsync(MakeRecord("agent-B"), ct);
        await _sut.RecordAsync(MakeRecord("agent-A"), ct);

        // Act
        var result = await _sut.GetByAgentAsync("agent-A", 50, ct);

        // Assert
        result.Should().HaveCount(2);
        result.All(r => r.AgentId == "agent-A").Should().BeTrue();
    }

    [Fact]
    public async Task GetByAgentAsync_IsCaseInsensitive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _sut.RecordAsync(MakeRecord("Agent-X"), ct);

        // Act
        var result = await _sut.GetByAgentAsync("agent-x", 50, ct);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void HashPrompt_ReturnsDeterministicHexString()
    {
        // Arrange
        const string input = "What is the capital of France?";

        // Act
        var hash1 = InMemoryAuditLog.HashPrompt(input);
        var hash2 = InMemoryAuditLog.HashPrompt(input);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public async Task RecordAsync_IsThreadSafe()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var tasks = Enumerable.Range(0, 50)
            .Select(i => _sut.RecordAsync(MakeRecord($"agent-{i}"), ct).AsTask());
        await Task.WhenAll(tasks);

        var result = await _sut.GetRecentAsync(100, ct);

        // Assert
        result.Should().HaveCount(50);
    }
}
