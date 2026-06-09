using FluentAssertions;
using Sentium.Sentinel.Application.RateLimiting;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class InMemoryRateLimitStoreTests
{
    private readonly InMemoryRateLimitStore _sut = new();

    [Fact]
    public void TryConsume_ReturnsTrue_WhenUnderLimit()
    {
        // Arrange
        var window = TimeSpan.FromMinutes(1);

        // Act
        var result = _sut.TryConsume("agent-1", window, 10);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ReturnsFalse_WhenLimitReached()
    {
        // Arrange
        var window = TimeSpan.FromMinutes(1);
        const int max = 3;

        // Consume up to limit
        for (var i = 0; i < max; i++)
        {
            _sut.TryConsume("agent-X", window, max);
        }

        // Act - one more exceeds the limit
        var result = _sut.TryConsume("agent-X", window, max);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryConsume_IsolatesAgents_ByAgentId()
    {
        // Arrange
        var window = TimeSpan.FromMinutes(1);

        // Consume all for agent-A
        _sut.TryConsume("agent-A", window, 1);
        _sut.TryConsume("agent-A", window, 1); // over limit

        // Act - agent-B should still be allowed
        var result = _sut.TryConsume("agent-B", window, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetCurrentCount_ReturnsZero_ForUnknownAgent()
    {
        // Act
        var count = _sut.GetCurrentCount("unknown-agent", TimeSpan.FromMinutes(1));

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetCurrentCount_ReflectsCurrentWindowCount()
    {
        // Arrange
        var window = TimeSpan.FromMinutes(1);
        _sut.TryConsume("counting-agent", window, 100);
        _sut.TryConsume("counting-agent", window, 100);

        // Act
        var count = _sut.GetCurrentCount("counting-agent", window);

        // Assert
        count.Should().Be(2);
    }
}
