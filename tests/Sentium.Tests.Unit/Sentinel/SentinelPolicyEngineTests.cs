using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class SentinelPolicyEngineTests
{
    private readonly IAuditLog _auditLog = Substitute.For<IAuditLog>();
    private readonly ILogger<SentinelPolicyEngine> _logger = NullLogger<SentinelPolicyEngine>.Instance;

    private static PolicyRequest MakeRequest(string action = "read", string resourceId = "docs/1") =>
        new()
        {
            AgentId = "agent-test",
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = resourceId,
            Action = action,
            OriginalUserPrompt = "test prompt",
            CorrelationId = Guid.NewGuid().ToString()
        };

    private SentinelPolicyEngine MakeEngine(params IPdpPolicy[] policies) =>
        new(policies, _auditLog, _logger);

    [Fact]
    public async Task EvaluateAsync_ReturnsAllow_WhenNoPoliciesDeny()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var engine = MakeEngine();

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.Allowed.Should().BeTrue();
        result.Effect.Should().Be(PolicyEffect.Allow);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDeny_WhenFirstPolicyDenies()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var denyingPolicy = Substitute.For<IPdpPolicy>();
        denyingPolicy.Name.Returns("TestDeny");
        denyingPolicy.EvaluateAsync(Arg.Any<PolicyRequest>(), ct)
            .Returns(PolicyDecision.Deny("blocked", Guid.Empty, ["TestDeny"]));

        var secondPolicy = Substitute.For<IPdpPolicy>();

        var engine = MakeEngine(denyingPolicy, secondPolicy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.Allowed.Should().BeFalse();
        await secondPolicy.DidNotReceive().EvaluateAsync(Arg.Any<PolicyRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_WritesAuditRecord_OnEveryEvaluation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var engine = MakeEngine();

        // Act
        await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert - the engine writes the audit with a non-cancellable token (CancellationToken.None)
        // so forensic records survive request cancellation; match on any token.
        await _auditLog.Received(1).RecordAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_FailsClosed_WhenPolicyThrowsException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var throwingPolicy = Substitute.For<IPdpPolicy>();
        throwingPolicy.Name.Returns("ThrowingPolicy");
        throwingPolicy.EvaluateAsync(Arg.Any<PolicyRequest>(), ct)
            .ThrowsAsync(new InvalidOperationException("unexpected!"));

        var engine = MakeEngine(throwingPolicy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.Allowed.Should().BeFalse();
        result.Risk.Should().Be(PolicyRiskLevel.Critical);
    }

    [Fact]
    public async Task EvaluateAsync_CapturesAlignmentVerdict_WhenPolicyReturnsOne()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var policy = Substitute.For<IPdpPolicy>();
        policy.Name.Returns("SemanticPolicy");
        policy.EvaluateAsync(Arg.Any<PolicyRequest>(), ct)
            .Returns(PolicyDecision.Allow(Guid.Empty, ["SemanticPolicy"]) with
            {
                AlignmentVerdict = "Aligned"
            });

        var engine = MakeEngine(policy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.AlignmentVerdict.Should().Be("Aligned");
    }

    [Fact]
    public async Task EvaluateAsync_LockdownShortCircuits_BeforeLaterPolicies()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var lockdown = new LockdownPolicy(
            new FakePdpRuntimeSettingsProvider(new PdpRuntimeSettings { LockdownMode = true }));

        var laterPolicy = Substitute.For<IPdpPolicy>();
        laterPolicy.Name.Returns("Later");

        // Lockdown registered first, mirroring production registration order.
        var engine = MakeEngine(lockdown, laterPolicy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        result.Allowed.Should().BeFalse();
        result.Risk.Should().Be(PolicyRiskLevel.Critical);
        await laterPolicy.DidNotReceive().EvaluateAsync(Arg.Any<PolicyRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var engine = MakeEngine();

        // Act
        var act = async () => await engine.EvaluateAsync(null!, ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_AuditRecord_ContainsCorrectAgentId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        AuditRecord? captured = null;
        _auditLog.RecordAsync(Arg.Do<AuditRecord>(r => captured = r), Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);

        var engine = MakeEngine();
        var request = MakeRequest();

        // Act
        await engine.EvaluateAsync(request, ct);

        // Assert
        captured.Should().NotBeNull();
        captured!.AgentId.Should().Be(request.AgentId);
    }
}
