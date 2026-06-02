using FluentAssertions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class InvariantGuardPolicyTests
{
    private readonly InvariantGuardPolicy _sut = new(Options.Create(new PdpOptions()));

    private static PolicyRequest MakeRequest(string action = "read", string resourceId = "docs/report-1") =>
        new()
        {
            AgentId = "agent-1",
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = resourceId,
            Action = action,
            OriginalUserPrompt = "test prompt",
            CorrelationId = Guid.NewGuid().ToString()
        };

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenRequestIsClean()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("read", "documents/report-1");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenWildcardResourceId_Star()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("read", "*");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.DenyWithAlert);
        result.Risk.Should().Be(PolicyRiskLevel.Critical);
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenWildcardResourceId_All()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("read", "all");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Effect.Should().Be(PolicyEffect.DenyWithAlert);
    }

    [Fact]
    public async Task EvaluateAsync_Denies_WhenLockdownModeAndWriteAction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var opts = new PdpOptions { LockdownMode = true };
        var sut = new InvariantGuardPolicy(Options.Create(opts));
        var request = MakeRequest("write");

        // Act
        var result = await sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_AllowsRead_WhenLockdownModeActive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var opts = new PdpOptions { LockdownMode = true };
        var sut = new InvariantGuardPolicy(Options.Create(opts));
        var request = MakeRequest("read");

        // Act
        var result = await sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenForbiddenAction_Delete()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("delete", "docs/report-1");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.DenyWithAlert);
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenForbiddenAction_Drop()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("drop", "table-1");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenProtectedResourcePrefix()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("read", ".env");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
        result.Effect.Should().Be(PolicyEffect.DenyWithAlert);
    }

    [Fact]
    public async Task EvaluateAsync_DeniesWithAlert_WhenSentinelWriteAccess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest("write", "sentinel-config");

        // Act
        var result = await _sut.EvaluateAsync(request, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Allowed.Should().BeFalse();
    }
}
