using FluentAssertions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class ProtectedResourcePolicyTests
{
    private static ProtectedResourcePolicy MakePolicy() =>
        new(Options.Create(new PdpOptions()));

    private static PolicyRequest MakeRequest(
        string action = "read",
        string resourceId = "docs/1",
        string skillName = "read_file") =>
        new()
        {
            AgentId = "agent-1",
            SkillName = skillName,
            ResourceType = ResourceType.File,
            ResourceId = resourceId,
            Action = action,
            OriginalUserPrompt = "test",
            CorrelationId = Guid.NewGuid().ToString()
        };

    [Theory]
    [InlineData(".env")]
    [InlineData("config/appsettings.json")]
    [InlineData("/etc/passwd")]
    [InlineData("keys/private_key.pem")]
    public async Task EvaluateAsync_DeniesProtectedPaths(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: resourceId), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Risk.Should().Be(PolicyRiskLevel.High);
        result.TriggeredPolicies.Should().Contain("ProtectedResource");
    }

    [Theory]
    [InlineData("delete", "docs/1")]
    [InlineData("write", "DROP TABLE users")]
    [InlineData("execute", "truncate logs")]
    public async Task EvaluateAsync_DeniesForbiddenActions(string action, string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(action: action, resourceId: resourceId), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("delete_file")]        // '_' is a token boundary, unlike regex \b
    [InlineData("force-drop")]         // '-' is a token boundary
    public async Task EvaluateAsync_DeniesForbiddenVerb_AcrossSeparatorBoundaries(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: resourceId), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("metrics/droplet-report.json")]   // "drop" only as a substring, not a whole token
    [InlineData("ui/dropdown-config.json")]
    public async Task EvaluateAsync_AllowsForbiddenVerbAsSubstring(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(action: "read", resourceId: resourceId), ct);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("C:/Windows/System32/config/SAM")]   // forward slashes vs configured backslash prefix
    [InlineData("c:\\windows\\system32\\drivers")]    // case-insensitive backslash variant
    public async Task EvaluateAsync_DeniesProtectedPath_RegardlessOfSeparatorStyle(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: resourceId), ct);

        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Risk.Should().Be(PolicyRiskLevel.High);
        result.TriggeredPolicies.Should().Contain("ProtectedResource");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_ForBenignRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(action: "read", resourceId: "reports/summary.md"), ct);

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
