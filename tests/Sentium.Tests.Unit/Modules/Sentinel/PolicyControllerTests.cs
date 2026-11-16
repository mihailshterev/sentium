using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Features.Engine;
using Sentium.Sentinel.Features.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class PolicyControllerTests
{
    private readonly InMemoryAuditLog _auditLog = new();
    private readonly PolicyController _controller;

    public PolicyControllerTests()
    {
        _controller = new PolicyController(_auditLog);
    }

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
}
