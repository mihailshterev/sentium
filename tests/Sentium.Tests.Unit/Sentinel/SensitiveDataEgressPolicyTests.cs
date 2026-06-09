using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class SensitiveDataEgressPolicyTests
{
    private static SensitiveDataEgressPolicy MakePolicy(PdpOptions? options = null) =>
        new(Options.Create(options ?? new PdpOptions()), NullLogger<SensitiveDataEgressPolicy>.Instance);

    private static PolicyRequest MakeRequest(
        string action = "write",
        string resourceId = "memory/note",
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new()
        {
            AgentId = "agent-1",
            SkillName = "store_memory",
            ResourceType = ResourceType.Memory,
            ResourceId = resourceId,
            Action = action,
            OriginalUserPrompt = "remember this",
            CorrelationId = Guid.NewGuid().ToString(),
            Metadata = metadata ?? new Dictionary<string, string>()
        };

    [Fact]
    public async Task EvaluateAsync_DeniesAwsKey_InResourceId()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "my key is AKIAIOSFODNN7EXAMPLE"), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.DenyWithAlert);
        result.Risk.Should().Be(PolicyRiskLevel.High);
        result.TriggeredPolicies.Should().Contain("SensitiveDataEgress");
    }

    [Fact]
    public async Task EvaluateAsync_DeniesPrivateKey_InMetadataPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();
        var metadata = new Dictionary<string, string>
        {
            ["payload"] = "here is the file:\n-----BEGIN RSA PRIVATE KEY-----\nMIIabc..."
        };

        var result = await sut.EvaluateAsync(MakeRequest(metadata: metadata), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_ForCleanWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "The user prefers dark mode."), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_SkipsReadActions()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        // Even with a secret present, a read/search action is out of scope for egress.
        var result = await sut.EvaluateAsync(MakeRequest(action: "search", resourceId: "AKIAIOSFODNN7EXAMPLE"), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenScanDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(new PdpOptions { EgressScanEnabled = false });

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "AKIAIOSFODNN7EXAMPLE"), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var act = async () => await sut.EvaluateAsync(null!, ct);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
