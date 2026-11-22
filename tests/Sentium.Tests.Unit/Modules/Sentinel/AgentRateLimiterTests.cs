using Sentium.Sentinel.Features.RateLimiting;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class AgentRateLimiterTests : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly AgentRateLimiter _sut = new();

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Allows_UpToTheLimit_ThenDenies()
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.True(_sut.TryAcquire("agent-x", Window, 3));
        }

        Assert.False(_sut.TryAcquire("agent-x", Window, 3));
    }

    [Fact]
    public void KeepsSeparateBudgets_PerKey()
    {
        Assert.True(_sut.TryAcquire("agent-a", Window, 1));
        Assert.False(_sut.TryAcquire("agent-a", Window, 1));

        Assert.True(_sut.TryAcquire("agent-b", Window, 1));
    }

    [Fact]
    public void MatchesKeys_IgnoringCase()
    {
        Assert.True(_sut.TryAcquire("Agent-A", Window, 1));

        Assert.False(_sut.TryAcquire("agent-a", Window, 1));
    }

    [Fact]
    public void ARaisedLimit_AppliesToTheNextRequest()
    {
        Assert.True(_sut.TryAcquire("agent-y", Window, 1));
        Assert.False(_sut.TryAcquire("agent-y", Window, 1));

        Assert.True(_sut.TryAcquire("agent-y", Window, 2));
    }
}
