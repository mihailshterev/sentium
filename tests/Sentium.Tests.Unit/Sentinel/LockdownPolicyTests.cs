using FluentAssertions;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class LockdownPolicyTests
{
    private static LockdownPolicy MakePolicy(bool lockdown) =>
        new(new FakePdpRuntimeSettingsProvider(new PdpRuntimeSettings { LockdownMode = lockdown }));

    private static PolicyRequest MakeRequest() =>
        new()
        {
            AgentId = "agent-1",
            SkillName = "knowledge_base_search",
            ResourceType = ResourceType.VectorDb,
            ResourceId = "docs/1",
            Action = "search",
            OriginalUserPrompt = "find docs",
            CorrelationId = Guid.NewGuid().ToString()
        };

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenLockdownDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: false);

        var result = await sut.EvaluateAsync(MakeRequest(), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithCriticalAlert_WhenLockdownEnabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: true);

        var result = await sut.EvaluateAsync(MakeRequest(), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.DenyWithAlert);
        result.Risk.Should().Be(PolicyRiskLevel.Critical);
        result.TriggeredPolicies.Should().Contain("Lockdown");
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: true);

        var act = async () => await sut.EvaluateAsync(null!, ct);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
