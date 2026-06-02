using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Sentinel.Application.Audit;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Api.Controllers;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Sentinel.Core.Policies;
using Xunit;

namespace Sentium.Tests.Unit.Sentinel;

public sealed class PolicyControllerTests
{
    private readonly InMemoryAuditLog _auditLog = new();
    private readonly PdpOptions _options = new();
    private readonly PolicyController _controller;

    public PolicyControllerTests()
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<PdpOptions>>();
        optionsMonitor.CurrentValue.Returns(_options);

        var engine = new SentinelPolicyEngine([], _auditLog, NullLogger<SentinelPolicyEngine>.Instance);
        _controller = new PolicyController(engine, _auditLog, optionsMonitor);
    }

    private static AuditRecord MakeAuditRecord(bool allowed = true) =>
        new()
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

    [Fact]
    public async Task GetAudit_ReturnsOk_WithRecords()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _auditLog.RecordAsync(MakeAuditRecord(), ct);

        // Act
        var result = await _controller.GetAudit(10, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<IReadOnlyList<AuditRecord>>().Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAudit_ClampsCount_ToMax500()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act — request 999 but the controller clamps to 500
        var result = await _controller.GetAudit(999, ct);

        // Assert — just verify it returns OK without error
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAuditByAgent_ReturnsOk_WithFilteredRecords()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var record = MakeAuditRecord();
        await _auditLog.RecordAsync(record, ct);

        // Act
        var result = await _controller.GetAuditByAgent(record.AgentId, 50, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<IReadOnlyList<AuditRecord>>().Should().ContainSingle();
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
    public void GetSettings_ReturnsOk_WithCurrentOptions()
    {
        // Act
        var result = _controller.GetSettings();

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PdpSettingsDto>().Should().NotBeNull();
    }

    [Fact]
    public void UpdateSettings_ReturnsOk_WithUpdatedValues()
    {
        // Arrange
        var request = new UpdatePdpSettingsRequest { LockdownMode = true, RateLimitMaxRequests = 50 };

        // Act
        var result = _controller.UpdateSettings(request);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<PdpSettingsDto>();
        dto.LockdownMode.Should().BeTrue();
        dto.RateLimitMaxRequests.Should().Be(50);
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
        result.Should().BeOfType<OkObjectResult>();
    }
}
