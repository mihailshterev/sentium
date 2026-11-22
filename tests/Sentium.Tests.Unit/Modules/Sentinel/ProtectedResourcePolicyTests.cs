using Microsoft.Extensions.Options;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Features.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class ProtectedResourcePolicyTests
{
    private static ProtectedResourcePolicy MakePolicy() =>
        new(Options.Create(new PdpOptions()));

    private static PolicyEvaluationRequest MakeRequest(
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

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
        Assert.Equal(PolicyRiskLevel.High, result.Risk);
        Assert.Contains("ProtectedResource", result.TriggeredPolicies);
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

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
    }

    [Theory]
    [InlineData("delete_file")]        // '_' is a token boundary, unlike regex \b
    [InlineData("force-drop")]         // '-' is a token boundary
    public async Task EvaluateAsync_DeniesForbiddenVerb_AcrossSeparatorBoundaries(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: resourceId), ct);

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
    }

    [Theory]
    [InlineData("metrics/droplet-report.json")]   // "drop" only as a substring, not a whole token
    [InlineData("ui/dropdown-config.json")]
    public async Task EvaluateAsync_AllowsForbiddenVerbAsSubstring(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(action: "read", resourceId: resourceId), ct);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("C:/Windows/System32/config/SAM")]   // forward slashes vs configured backslash prefix
    [InlineData("c:\\windows\\system32\\drivers")]    // case-insensitive backslash variant
    public async Task EvaluateAsync_DeniesProtectedPath_RegardlessOfSeparatorStyle(string resourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(resourceId: resourceId), ct);

        Assert.NotNull(result);
        Assert.False(result!.Allowed);
        Assert.Equal(PolicyRiskLevel.High, result.Risk);
        Assert.Contains("ProtectedResource", result.TriggeredPolicies);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_ForBenignRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = MakePolicy();

        var result = await sut.EvaluateAsync(MakeRequest(action: "read", resourceId: "reports/summary.md"), ct);

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
