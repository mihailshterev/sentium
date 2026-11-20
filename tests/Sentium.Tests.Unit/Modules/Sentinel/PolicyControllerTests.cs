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
        Assert.Single(Assert.IsAssignableFrom<PagedResponse<AuditRecord>>(Assert.IsType<OkObjectResult>(result).Value).Items);
    }

    [Fact]
    public async Task GetAudit_ClampsPageSize_ToMax100()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act - request 999 but the controller clamps page size to 100
        var result = await _controller.GetAudit(1, 999, ct);

        // Assert
        Assert.Equal(100, Assert.IsAssignableFrom<PagedResponse<AuditRecord>>(Assert.IsType<OkObjectResult>(result).Value).PageSize);
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
        Assert.Single(Assert.IsAssignableFrom<PagedResponse<AuditRecord>>(Assert.IsType<OkObjectResult>(result).Value).Items);
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk_WithZeroCounts_WhenEmpty()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _controller.GetAuditStats(ct);

        // Assert
        var stats = Assert.IsAssignableFrom<AuditStatsDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(0, stats.Total);
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
        var stats = Assert.IsAssignableFrom<AuditStatsDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Allowed);
        Assert.Equal(1, stats.Denied);
    }
}
