using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Features.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class SensitiveDataEgressPolicyTests
{
    private static SensitiveDataEgressPolicy MakePolicy(PdpOptions? options = null) =>
        new(Options.Create(options ?? new PdpOptions()), NullLogger<SensitiveDataEgressPolicy>.Instance);

    private static PolicyEvaluationRequest MakeRequest(
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

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
        Assert.Equal(PolicyEffect.DenyWithAlert, result.Effect);
        Assert.Equal(PolicyRiskLevel.High, result.Risk);
        Assert.Contains("SensitiveDataEgress", result.TriggeredPolicies);
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

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_ForCleanWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "The user prefers dark mode."), ct);

        Assert.Null(result);
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

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
        Assert.Equal(PolicyRiskLevel.High, result.Risk);
        Assert.Contains("SensitiveDataEgress", result.TriggeredPolicies);
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

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_DeniesOpenAiKey_ButNotShortSkPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();
        var realKey = new Dictionary<string, string> { ["payload"] = "key=sk-proj-abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGH" };
        var falsePositive = new Dictionary<string, string> { ["payload"] = "the sku is sk-1234 for that item" };

        Assert.NotNull(await sut.EvaluateAsync(MakeRequest(metadata: realKey), ct));
        Assert.Null(await sut.EvaluateAsync(MakeRequest(metadata: falsePositive), ct));
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotLeakMatchedPattern_InReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "my key is AKIAIOSFODNN7EXAMPLE"), ct);

        Assert.NotNull(result);
        Assert.DoesNotContain("AKIA", result!.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("[0-9A-Z]", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_SkipsReadActions()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        // Even with a secret present, a read/search action is out of scope for egress.
        var result = await sut.EvaluateAsync(MakeRequest(action: "search", resourceId: "AKIAIOSFODNN7EXAMPLE"), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenScanDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy(new PdpOptions { EgressScanEnabled = false });

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: "AKIAIOSFODNN7EXAMPLE"), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var act = async () => await sut.EvaluateAsync(null!, ct);

        await Assert.ThrowsAnyAsync<ArgumentNullException>(act);
    }
}
