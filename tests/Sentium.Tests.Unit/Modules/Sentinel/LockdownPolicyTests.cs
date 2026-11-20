using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class LockdownPolicyTests
{
    private static LockdownPolicy MakePolicy(bool lockdown) =>
        new(new FakePdpRuntimeSettingsProvider(new PdpRuntimeSettings { LockdownMode = lockdown }));

    private static PolicyRequest MakeRequest()
    {
        return new()
        {
            AgentId = "agent-1",
            SkillName = "knowledge_base_search",
            ResourceType = ResourceType.VectorDb,
            ResourceId = "docs/1",
            Action = "search",
            OriginalUserPrompt = "find docs",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenLockdownDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: false);

        var result = await sut.EvaluateAsync(MakeRequest(), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithCriticalAlert_WhenLockdownEnabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: true);

        var result = await sut.EvaluateAsync(MakeRequest(), ct);

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
        Assert.Equal(PolicyEffect.DenyWithAlert, result.Effect);
        Assert.Equal(PolicyRiskLevel.Critical, result.Risk);
        Assert.Contains("Lockdown", result.TriggeredPolicies);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(lockdown: true);

        var act = async () => await sut.EvaluateAsync(null!, ct);

        await Assert.ThrowsAnyAsync<ArgumentNullException>(act);
    }
}
