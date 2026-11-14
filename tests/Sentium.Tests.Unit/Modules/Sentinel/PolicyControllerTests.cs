using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Core.Results;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Features.Engine;
using Sentium.Sentinel.Features.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class PolicyControllerTests : IDisposable
{
    private readonly InMemoryAuditLog _auditLog = new();
    private readonly ServiceProvider _services;
    private readonly PolicyController _controller;

    public PolicyControllerTests()
    {
        // The controller evaluates through the in-process decision point, which runs the engine in its own scope.
        _services = new ServiceCollection()
            .AddSingleton<IAuditLog>(_auditLog)
            .AddSingleton<ILogger<SentinelPolicyEngine>>(NullLogger<SentinelPolicyEngine>.Instance)
            .AddScoped<SentinelPolicyEngine>()
            .BuildServiceProvider();

        var policyDecisionPoint = new PolicyDecisionPoint(_services.GetRequiredService<IServiceScopeFactory>());
        _controller = new PolicyController(policyDecisionPoint, _auditLog);
    }

    public void Dispose() => _services.Dispose();

    private static AuditRecord MakeAuditRecord(bool allowed = true)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "agent-1",
            SkillName = "TestSkill",
            ResourceType = ResourceType.File,
            ResourceId = "docs/1",
            Action = "read",
            UserPromptHash = "abc",
            CorrelationId = Guid.NewGuid().ToString(),
            EvaluationDurationMs = 5,
            Allowed = allowed,
            Effect = allowed ? PolicyEffect.Allow : PolicyEffect.Deny,
            Reason = "test",
            Risk = PolicyRiskLevel.Low,
            TriggeredPolicies = []
        };
    }

    [Fact]
    public async Task GetAudit_ReturnsOk_WithRecords()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _auditLog.RecordAsync(MakeAuditRecord(), ct);

        // Act
        var result = await _controller.GetAudit(1, 10, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PagedResponse<AuditRecord>>().Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAudit_ClampsPageSize_ToMax100()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act - request 999 but the controller clamps page size to 100
        var result = await _controller.GetAudit(1, 999, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PagedResponse<AuditRecord>>().PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAuditByAgent_ReturnsOk_WithFilteredRecords()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var record = MakeAuditRecord();
        await _auditLog.RecordAsync(record, ct);

        // Act
        var result = await _controller.GetAuditByAgent(record.AgentId, 1, 50, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PagedResponse<AuditRecord>>().Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk_WithZeroCounts_WhenEmpty()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _controller.GetAuditStats(ct);

        // Assert
        var stats = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<AuditStatsDto>();
        stats.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetAuditStats_ComputesCorrectCounts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _auditLog.RecordAsync(MakeAuditRecord(allowed: true), ct);
        await _auditLog.RecordAsync(MakeAuditRecord(allowed: false), ct);

        // Act
        var result = await _controller.GetAuditStats(ct);

        // Assert
        var stats = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<AuditStatsDto>();
        stats.Total.Should().Be(2);
        stats.Allowed.Should().Be(1);
        stats.Denied.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsOk_WithAllowDecision()
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
        var result = await _controller.EvaluateAsync(body, ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PolicyEvaluationResponse>().Allowed.Should().BeTrue();
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
        var result = await _controller.EvaluateAsync(body, ct);

        // Assert - fail-closed: an invalid request is a deny decision, not an exception.
        var decision = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PolicyEvaluationResponse>();
        decision.Allowed.Should().BeFalse();
        decision.Effect.Should().Be("Deny");
    }
}
