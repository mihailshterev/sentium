using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Features.Engine;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class PolicyDecisionPointTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly PolicyDecisionPoint _sut;

    public PolicyDecisionPointTests()
    {
        // The decision point runs the engine in its own scope.
        _services = new ServiceCollection()
            .AddSingleton<IAuditLog>(new InMemoryAuditLog())
            .AddSingleton<ILogger<SentinelPolicyEngine>>(NullLogger<SentinelPolicyEngine>.Instance)
            .AddScoped<SentinelPolicyEngine>()
            .BuildServiceProvider();

        _sut = new PolicyDecisionPoint(_services.GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task EvaluateAsync_ReturnsAllowDecision()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var body = new PolicyEvaluationRequest
        {
            AgentId = "agent-1",
            SkillName = "TestSkill",
            ResourceType = "File",
            ResourceId = "docs/report-1",
            Action = "read",
            OriginalUserPrompt = "Show me the report",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var decision = await _sut.EvaluateAsync(body, ct);

        // Assert
        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_DeniesUnknownResourceType_InsteadOfThrowing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var body = new PolicyEvaluationRequest
        {
            AgentId = "agent-1",
            SkillName = "TestSkill",
            ResourceType = "NotAResourceType",
            ResourceId = "x",
            Action = "read",
            OriginalUserPrompt = "Show me the report",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var decision = await _sut.EvaluateAsync(body, ct);

        // Assert - fail-closed: an invalid request is a deny decision, not an exception.
        decision.Allowed.Should().BeFalse();
        decision.Effect.Should().Be("Deny");
    }
}
