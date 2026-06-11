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

    [Theory]
    [InlineData("4111111111111111")]        // Visa test number
    [InlineData("4111 1111 1111 1111")]     // separated with spaces
    [InlineData("5500-0000-0000-0004")]     // Mastercard, separated with hyphens
    [InlineData("378282246310005")]         // Amex (15 digits)
    public async Task EvaluateAsync_DeniesLuhnValidCardNumber(string card)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();
        var metadata = new Dictionary<string, string> { ["payload"] = $"please charge {card} now" };

        var result = await sut.EvaluateAsync(MakeRequest(metadata: metadata), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Risk.Should().Be(PolicyRiskLevel.High);
        result.TriggeredPolicies.Should().Contain("SensitiveDataEgress");
    }

    [Theory]
    [InlineData("1700000000000")]           // 13-digit epoch-millis timestamp (starts with 1)
    [InlineData("9876543210987654")]        // 16-digit id that fails the Luhn checksum
    [InlineData("4111111111111112")]        // card-shaped but invalid Luhn check digit
    [InlineData("order 1234567890 shipped")] // ordinary numeric id
    public async Task EvaluateAsync_ReturnsNull_ForNonCardNumerics(string payload)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();
        var metadata = new Dictionary<string, string> { ["payload"] = payload };

        var result = await sut.EvaluateAsync(MakeRequest(metadata: metadata), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesOpenAiKey_ButNotShortSkPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();
        var realKey = new Dictionary<string, string> { ["payload"] = "key=sk-proj-abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGH" };
        var falsePositive = new Dictionary<string, string> { ["payload"] = "the sku is sk-1234 for that item" };

        (await sut.EvaluateAsync(MakeRequest(metadata: realKey), ct)).Should().NotBeNull();
        (await sut.EvaluateAsync(MakeRequest(metadata: falsePositive), ct)).Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotLeakMatchedPattern_InReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "my key is AKIAIOSFODNN7EXAMPLE"), ct);

        result.Should().NotBeNull();
        result!.Reason.Should().NotContain("AKIA");
        result.Reason.Should().NotContain("[0-9A-Z]");
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
