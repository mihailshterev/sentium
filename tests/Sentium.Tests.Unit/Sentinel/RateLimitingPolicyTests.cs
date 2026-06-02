using FluentAssertions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Application.RateLimiting;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class RateLimitingPolicyTests
{
    private static RateLimitingPolicy MakePolicy(int max = 10, int windowSeconds = 60)
    {
        var opts = new PdpOptions { RateLimitMaxRequests = max, RateLimitWindowSeconds = windowSeconds };
        return new RateLimitingPolicy(new InMemoryRateLimitStore(), Options.Create(opts));
    }

    private static PolicyRequest MakeRequest(string agentId = "agent-1") =>
        new()
        {
            AgentId = agentId,
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = "docs/1",
            Action = "read",
            OriginalUserPrompt = "test",
            CorrelationId = Guid.NewGuid().ToString()
        };

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenUnderLimit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(max: 5);

        // Act
        var result = await sut.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDenyDecision_WhenLimitExceeded()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(max: 3);

        // Consume up to limit
        for (var i = 0; i < 3; i++)
        {
            await sut.EvaluateAsync(MakeRequest("burst-agent"), ct);
        }

        // Act — fourth request exceeds limit
        var result = await sut.EvaluateAsync(MakeRequest("burst-agent"), ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.Deny);
        result.Risk.Should().Be(PolicyRiskLevel.High);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        // Act
        var act = async () => await sut.EvaluateAsync(null!, ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
