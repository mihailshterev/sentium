using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Engine;
using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class SentinelPolicyEngineTests
{
    private readonly IAuditLog _auditLog = Substitute.For<IAuditLog>();
    private readonly ILogger<SentinelPolicyEngine> _logger = NullLogger<SentinelPolicyEngine>.Instance;

    private static PolicyEvaluationRequest MakeRequest(string action = "read", string resourceId = "docs/1")
    {
        return new()
        {
            AgentId = "agent-test",
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = resourceId,
            Action = action,
            OriginalUserPrompt = "test prompt",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

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
        Assert.True(result.Allowed);
        Assert.Equal(PolicyEffect.Allow, result.Effect);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDeny_WhenFirstPolicyDenies()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var denyingPolicy = Substitute.For<IPdpPolicy>();
        denyingPolicy.Name.Returns("TestDeny");
        denyingPolicy.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), ct)
            .Returns(PolicyDecision.Deny("blocked", Guid.Empty, ["TestDeny"]));

        var secondPolicy = Substitute.For<IPdpPolicy>();

        var engine = MakeEngine(denyingPolicy, secondPolicy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        Assert.False(result.Allowed);
        await secondPolicy.DidNotReceive().EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>());
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
        throwingPolicy.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), ct)
            .ThrowsAsync(new InvalidOperationException("unexpected!"));

        var engine = MakeEngine(throwingPolicy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal(PolicyRiskLevel.Critical, result.Risk);
    }

    [Fact]
    public async Task EvaluateAsync_CapturesAlignmentVerdict_WhenPolicyReturnsOne()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var policy = Substitute.For<IPdpPolicy>();
        policy.Name.Returns("SemanticPolicy");
        policy.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), ct)
            .Returns(PolicyDecision.Allow(Guid.Empty, ["SemanticPolicy"]) with
            {
                AlignmentVerdict = "Aligned"
            });

        var engine = MakeEngine(policy);

        // Act
        var result = await engine.EvaluateAsync(MakeRequest(), ct);

        // Assert
        Assert.Equal("Aligned", result.AlignmentVerdict);
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
        Assert.False(result.Allowed);
        Assert.Equal(PolicyRiskLevel.Critical, result.Risk);
        await laterPolicy.DidNotReceive().EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>());
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
        await Assert.ThrowsAnyAsync<ArgumentNullException>(act);
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
        Assert.NotNull(captured);
        Assert.Equal(request.AgentId, captured!.AgentId);
    }

    [Fact]
    public async Task EvaluateAsync_AuditsTheUser_AndRedactsThePayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var request = MakeRequest("write") with
        {
            UserId = userId,
            Payload = "AKIAIOSFODNN7EXAMPLE secret",
            Metadata = new Dictionary<string, string> { ["language"] = "Python" }
        };

        await MakeEngine().EvaluateAsync(request, ct);

        await _auditLog.Received(1).RecordAsync(
            Arg.Is<AuditRecord>(r =>
                r.UserId == userId
                && r.Metadata["language"] == "Python"
                && r.Metadata["payload"].StartsWith("[redacted: 27 chars, sha256 ", StringComparison.Ordinal)
                && !r.Metadata["payload"].Contains("AKIA", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }
}
