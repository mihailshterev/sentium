using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Praxis.Features.CodeExecution;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.CodeExecution;

/// <summary>
/// The sandbox execution history moved out of the (now stateless) Sandbox service into Praxis.
/// </summary>
public sealed class SandboxHistoryControllerTests
{
    private readonly IExecutionLogRepository _executionLog = Substitute.For<IExecutionLogRepository>();
    private readonly BlobServiceClient _blobClient = Substitute.For<BlobServiceClient>();
    private readonly SandboxController _controller;

    public SandboxHistoryControllerTests()
    {
        _controller = new SandboxController(
            _executionLog,
            _blobClient,
            Options.Create(new SandboxArtifactOptions()))
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
}
