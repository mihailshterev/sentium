using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Sandbox.Api.Controllers;
using Sentium.Sandbox.Core.Dtos;
using Sentium.Sandbox.Application;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Application.Sentinel;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;
using Xunit;

namespace Sentium.Tests.Unit.Sandbox;

public sealed class SandboxControllerTests
{
    private readonly ISandboxRunner _runner = Substitute.For<ISandboxRunner>();
    private readonly ISentinelGateway _sentinel = Substitute.For<ISentinelGateway>();
    private readonly IExecutionLogRepository _executionLog = Substitute.For<IExecutionLogRepository>();
    private readonly BlobServiceClient _blobClient = Substitute.For<BlobServiceClient>();
    private readonly SandboxController _controller;

    public SandboxControllerTests()
    {
        var orchestrator = new SandboxOrchestrator(
            _runner, _sentinel, _executionLog, NullLogger<SandboxOrchestrator>.Instance);

        _controller = new SandboxController(
            orchestrator,
            _executionLog,
            _blobClient,
            Options.Create(new SandboxOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static ExecutionLogEntry MakeLogEntry() =>
        new()
        {
            JobId = Guid.NewGuid(),
            ExecutedAt = DateTimeOffset.UtcNow,
            AgentId = "agent-1",
            CorrelationId = Guid.NewGuid().ToString(),
            Language = ExecutionLanguage.Python,
            Code = "print('hi')",
            Succeeded = true,
            ExitCode = 0,
            Output = "hi\n",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            SentinelAuditId = Guid.NewGuid(),
            DurationMs = 50
        };

    [Fact]
    public async Task GetExecutions_ReturnsOk_WithPagedResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entries = new List<ExecutionLogEntry> { MakeLogEntry() };
        _executionLog.GetPagedAsync(Arg.Any<ExecutionLogQuery>(), ct).Returns((entries, 1));

        // Act
        var result = await _controller.GetExecutions(1, 20, null, null, null, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExecutions_ClampsPageSize_ToMax100()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _executionLog.GetPagedAsync(Arg.Any<ExecutionLogQuery>(), ct).Returns((new List<ExecutionLogEntry>(), 0));

        // Act
        await _controller.GetExecutions(1, 999, null, null, null, ct);

        // Assert
        await _executionLog.Received(1).GetPagedAsync(
            Arg.Is<ExecutionLogQuery>(q => q.PageSize == 100), ct);
    }

    [Fact]
    public async Task GetExecutions_ClampsPage_ToMinimum1()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _executionLog.GetPagedAsync(Arg.Any<ExecutionLogQuery>(), ct).Returns((new List<ExecutionLogEntry>(), 0));

        // Act
        await _controller.GetExecutions(0, 20, null, null, null, ct);

        // Assert
        await _executionLog.Received(1).GetPagedAsync(
            Arg.Is<ExecutionLogQuery>(q => q.Page == 1), ct);
    }

    [Fact]
    public async Task GetExecution_ReturnsOk_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entry = MakeLogEntry();
        _executionLog.GetByIdAsync(entry.JobId, ct).Returns(entry);

        // Act
        var result = await _controller.GetExecution(entry.JobId, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExecution_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _executionLog.GetByIdAsync(id, ct).Returns((ExecutionLogEntry?)null);

        // Act
        var result = await _controller.GetExecution(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetStats_ReturnsOk_WithAggregatedCounts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _executionLog.GetStatsAsync(ct).Returns(new ExecutionLogStats(10, 7, 2, 1));

        // Act
        var result = await _controller.GetStats(ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenPolicyDenied()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var deniedResult = new ExecutionResult
        {
            Succeeded = false,
            ExitCode = -1,
            Output = string.Empty,
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = true,
            PolicyDenialReason = "blocked",
            JobId = Guid.NewGuid(),
            DurationMs = 0
        };

        _sentinel.AuthorizeExecutionAsync(Arg.Any<ExecutionRequest>(), ct)
            .Returns(SentinelAuthorizationResult.Deny("blocked", Guid.NewGuid()));

        var body = new SandboxExecutionRequest
        {
            Language = "Python",
            Code = "print('hi')",
            AgentId = "agent-1"
        };

        // Act
        var result = await _controller.ExecuteAsync(body, ct);

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOk_WhenExecutionSucceeds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var auditId = Guid.NewGuid();
        var successResult = new ExecutionResult
        {
            Succeeded = true,
            ExitCode = 0,
            Output = "hello\n",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            JobId = Guid.NewGuid(),
            DurationMs = 150
        };

        _sentinel.AuthorizeExecutionAsync(Arg.Any<ExecutionRequest>(), ct)
            .Returns(SentinelAuthorizationResult.Allow(auditId));
        _runner.RunAsync(Arg.Any<ExecutionRequest>(), ct).Returns(successResult);

        var body = new SandboxExecutionRequest
        {
            Language = "Python",
            Code = "print('hello')",
            AgentId = "agent-1"
        };

        // Act
        var result = await _controller.ExecuteAsync(body, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
